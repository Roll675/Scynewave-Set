using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditor.IMGUI.Controls;
using UnityEditorInternal;
using UnityEngine;
using VRC.Udon.Graph;

namespace CyanTrigger
{
    public class CyanTriggerActionTreeView : CyanTriggerScopedDataTreeView<CyanTriggerActionTreeView.ActionData>
    {
        public class ActionData : ActionInstanceRenderData
        {
            public bool IsExpanded;
        }

        public class ActionInstanceRenderData
        {
            public SerializedProperty Property;
            public CyanTriggerActionInfoHolder ActionInfo;
            public bool[] ExpandedInputs = new bool[0];
            public ReorderableList[] InputLists = new ReorderableList[0];
            public bool NeedsRedraws;

            public void ClearInputLists()
            {
                if (InputLists == null)
                {
                    return;
                }

                for (int i = 0; i < InputLists.Length; ++i)
                {
                    InputLists[i] = null;
                }
            }
        }

        private static Texture2D BoxOutline;
        
        private const float DefaultRowHeight = 20;
        private const float SpaceBetweenRowEditor = 6;
        private const float SpaceBetweenRowEditorSides = 6;

        private const float ExpandButtonSize = 16;

        private readonly AnimBool _showActions;
        public Action OnActionChanged;
        public Func<Type, int, List<CyanTriggerEditorVariableOption>> GetVariableOptions;
        
        private bool _delayRefreshRowHeight = false;
        private GUIStyle _helpBoxStyle;
        
        private static MultiColumnHeader CreateColumnHeader()
        {
            MultiColumnHeaderState.Column[] columns =
            {
                new MultiColumnHeaderState.Column
                {
                    minWidth = 50f, width = 100f, headerTextAlignment = TextAlignment.Center, canSort = false
                }
            };
            MultiColumnHeader multiColumnHeader = new MultiColumnHeader(new MultiColumnHeaderState(columns))
            {
                height = 0,
            };
            multiColumnHeader.ResizeToFit();
            
            return multiColumnHeader;
        }
        
        private static string GetElementDisplayName(SerializedProperty actionProperty)
        {
            CyanTriggerActionInfoHolder actionInfo = 
                CyanTriggerActionInfoHolder.GetActionInfoHolderFromProperties(actionProperty);
            return actionInfo.GetActionRenderingDisplayName(actionProperty);
        }
        
        private static int GetElementScopeDelta(SerializedProperty actionProperty)
        {
            CyanTriggerActionInfoHolder actionInfo = 
                CyanTriggerActionInfoHolder.GetActionInfoHolderFromProperties(actionProperty);
            return actionInfo.GetScopeDelta();
        }
        
        public CyanTriggerActionTreeView(
            SerializedProperty elements, 
            Action onActionChanged,
            Func<Type, int, List<CyanTriggerEditorVariableOption>> getVariableOptions) 
            : base(elements, CreateColumnHeader(), GetElementScopeDelta, GetElementDisplayName)
        {
            showBorder = true;
            rowHeight = DefaultRowHeight;
            showAlternatingRowBackgrounds = true;
            useScrollView = false;

            OnActionChanged = onActionChanged;

            _showActions = new AnimBool(true);
            _showActions.valueChanged.AddListener(Repaint);

            GetVariableOptions = getVariableOptions;

            if (BoxOutline == null)
            {
                BoxOutline = new Texture2D(3, 3);
                BoxOutline.alphaIsTransparency = true;
                BoxOutline.filterMode = FilterMode.Point;
                for (int y = 0; y < BoxOutline.height; ++y)
                {
                    for (int x = 0; x < BoxOutline.width; ++x)
                    {
                        Color color = x == 1 && y == 1 ? Color.clear : Color.gray;
                        BoxOutline.SetPixel(x, y, color);
                    }
                }
                BoxOutline.Apply();
            }
        }

        private ActionData GetOrCreateExpandData(int id, bool forceCreate = false)
        {
            var data = GetData(id);
            int index = GetItemIndex(id);
            if (forceCreate || data == null)
            {
                data = new ActionData();
                SetData(id, data);
            }

            if (data.ActionInfo == null)
            {
                FillDataForElement(data, index);
            }
            else
            {
                data.Property = index < ItemElements.Length ? 
                    ItemElements[index] : 
                    Elements.GetArrayElementAtIndex(index);
            }
            
            return data;
        }

        private void FillDataForElement(ActionData data, int index)
        {
            SerializedProperty property = index < ItemElements.Length ? 
                ItemElements[index] : 
                Elements.GetArrayElementAtIndex(index);
            
            data.ActionInfo = CyanTriggerActionInfoHolder.GetActionInfoHolderFromProperties(property);
            data.Property = property;

            var variables = data.ActionInfo.GetVariables();
            data.InputLists = new ReorderableList[variables.Length];
            data.ExpandedInputs = new bool[variables.Length];
            for (int cur = 0; cur < variables.Length; ++cur)
            {
                data.ExpandedInputs[cur] = true;
            }
        }

        private bool ShouldShowVariantSelector(ActionData actionData)
        {
            var definition = actionData.ActionInfo.definition;
            return definition == null ||
                !(definition.definitionType == CyanTriggerNodeDefinition.UdonDefinitionType.CyanTriggerSpecial ||
                  definition.definitionType == CyanTriggerNodeDefinition.UdonDefinitionType.CyanTriggerVariable ||
                  definition.definitionType == CyanTriggerNodeDefinition.UdonDefinitionType.Type);
        }
        
        private bool ItemCanExpand(ActionData actionData)
        {
            return ShouldShowVariantSelector(actionData) || actionData.ActionInfo.GetVariables().Length > 0;
        }

        public void UpdateAllItemDisplayNames()
        {
            for (int i = 0; i < Elements.arraySize; ++i)
            {
                if (Items[i] != null)
                {
                    Items[i].displayName = GetElementDisplayName(ItemElements[i]);
                }
            }
        }
        
        public void DoLayoutTree()
        {
            bool isUndo = (Event.current.type == EventType.ValidateCommand &&
                           Event.current.commandName == "UndoRedoPerformed");

            // TODO fix this by storing expanded value with properties. Everything else can be regenerated.
            if (isUndo)
            {
                foreach (var data in GetData())
                {
                    data.Item1.ActionInfo = null;
                }
                
                // TODO Clear data and reload once properties are stored.
            }
            
            _helpBoxStyle = new GUIStyle(EditorStyles.helpBox);
            
            bool showView = _showActions.target;
            
            CyanTriggerPropertyEditor.DrawFoldoutListHeader(
                new GUIContent("Actions (" + VisualSize +")"),
                ref showView,
                false,
                Elements.arraySize,
                null,
                false,
                null,
                false,
                true
                );
            _showActions.target = showView;
            
            if (!EditorGUILayout.BeginFadeGroup(_showActions.faded))
            {
                EditorGUILayout.EndFadeGroup();
                return;
            }
            
            if (Size != Elements.arraySize)
            {
                // TODO verify remapping
                Reload();
            }
            
            Rect treeRect = EditorGUILayout.BeginVertical();
            treeRect.y -= 2;
            treeRect.height = totalHeight + (Size == 0 ? DefaultRowHeight : 0);
            treeRect.x += 1;
            treeRect.width -= 2;
            GUILayout.Space(treeRect.height - 1);
            
            var listActionFooterIcons = new[]
            {
                new GUIContent("SDK2", "Add action from list of SDK2 actions"),
                EditorGUIUtility.TrIconContent("Favorite", "Add action from favorites actions"), //CustomSorting
                EditorGUIUtility.TrIconContent("Toolbar Plus More", "Add action from all actions"),
                EditorGUIUtility.TrIconContent("FilterByType", "Add Local Variable"),
                EditorGUIUtility.TrIconContent("TreeEditor.Duplicate", "Duplicate selected item"),
                EditorGUIUtility.TrIconContent("Toolbar Minus", "Remove selection from list")
            };
            
            bool hasSelection = HasSelection();
            CyanTriggerPropertyEditor.DrawButtonFooter(
                listActionFooterIcons, new Action[]
                {
                    AddNewActionFromSDK2List,
                    AddNewActionFromFavoriteList,
                    AddNewActionFromAllList,
                    AddLocalVariable,
                    DuplicateSelectedItems,
                    RemoveSelected
                },
                new []
                {
                    false, false, false, false, !hasSelection, !hasSelection
                });
            
            OnGUI(treeRect);
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndFadeGroup();
            
            if (_delayRefreshRowHeight)
            {
                _delayRefreshRowHeight = false;
                RefreshCustomRowHeights();
            }
        }
        
        protected override float GetCustomRowHeight(int row, TreeViewItem item)
        {
            float height = DefaultRowHeight;
            var scopedItem = (CyanTriggerScopedTreeItem) item;

            ActionData expandData = GetOrCreateExpandData(item.id);
            if (!ItemCanExpand(expandData))
            {
                return height;
            }

            if (expandData.IsExpanded)
            {
                int index = scopedItem.Index;
                // Should show drop down
                bool shouldShowVariants = ShouldShowVariantSelector(expandData);
                if (shouldShowVariants)
                {
                    height += DefaultRowHeight;
                }

                float inputHeight = CyanTriggerPropertyEditor.GetHeightForActionInstanceInputEditors(
                    expandData, 
                    type => GetVariableOptions(type, index));

                height += inputHeight;
                
                // Add separator spacing
                if (shouldShowVariants && inputHeight > 5)
                {
                    height += SpaceBetweenRowEditor * 2;
                }
            }

            if (height > DefaultRowHeight)
            {
                height += SpaceBetweenRowEditor * 2;
            }

            return height;
        }
        
        protected override void RowGUI(RowGUIArgs args)
        {
            Rect rowRect = args.rowRect;
            
            // Draw action name in cell
            Rect cellRect = args.GetCellRect(0);
            cellRect.height = DefaultRowHeight;
            CenterRectUsingSingleLineHeight(ref cellRect);

            Rect expandButtonRect = new Rect(cellRect.xMax - ExpandButtonSize, cellRect.y, cellRect.height,
                ExpandButtonSize);
            cellRect.width -= ExpandButtonSize + SpaceBetweenRowEditorSides;
            
            args.rowRect = cellRect;
            base.RowGUI(args);
            
            // Draw expand button on right of the row.
            var item = (CyanTriggerScopedTreeItem)args.item;
            var data = GetOrCreateExpandData(item.id);
            
            // TODO find better icons that don't look like add/remove
            GUIContent expandIcon = data.IsExpanded
                ? EditorGUIUtility.TrIconContent("Toolbar Minus", "Close Action Editor")
                : EditorGUIUtility.TrIconContent("Toolbar Plus", "Expand Action Editor");

            if (!ItemCanExpand(data))
            {
                return;
            }
            
            // TODO find better button style that fits icon and has edges?
            if (GUI.Button(expandButtonRect, expandIcon, "RL FooterButton"))
            {
                data.IsExpanded = !data.IsExpanded;
                _delayRefreshRowHeight = true;
            }

            if (!data.IsExpanded || _delayRefreshRowHeight)
            {
                return;
            }
            
            bool isUndo = (Event.current.type == EventType.ValidateCommand &&
                           Event.current.commandName == "UndoRedoPerformed");
            if (isUndo)
            {
                data.NeedsRedraws = true;
                ClearInputList(data);
            }

            Rect fullRect = rowRect;
            // Remove top area from Row
            rowRect.height -= DefaultRowHeight;
            rowRect.y += DefaultRowHeight;
            
            if (Event.current.type == EventType.Repaint)
            {
                // Draw background to overwrite the selection blue
                GUIStyle backgroundStyle = args.row % 2 == 1 ? DefaultStyles.backgroundOdd : DefaultStyles.backgroundEven;
                backgroundStyle.Draw(rowRect, false, false, false, false);
            }
            
            // Draw an outline around the element to emphasize what you are editing.
            var boxStyle = new GUIStyle
            {
                border = new RectOffset(1, 1, 1, 1), 
                normal = {background = BoxOutline}
            };
            GUI.Box(fullRect, GUIContent.none, boxStyle);

            // Draw rect around area to separate it from everything else
            rowRect.x += SpaceBetweenRowEditorSides;
            rowRect.width -= SpaceBetweenRowEditorSides * 2;
            rowRect.y += SpaceBetweenRowEditor;
            rowRect.height -= SpaceBetweenRowEditor * 2;
            
            if (Event.current.type == EventType.Repaint)
            {
                _helpBoxStyle.Draw(rowRect, false, false, false, false); 
            }

            rowRect.height -= SpaceBetweenRowEditor;
            rowRect.y += SpaceBetweenRowEditor;

            rowRect.x += SpaceBetweenRowEditorSides;
            rowRect.width -= SpaceBetweenRowEditorSides * 2;

            if (ShouldShowVariantSelector(data))
            {
                Rect variantRect = new Rect(rowRect);
                variantRect.height = DefaultRowHeight;
                
                DrawVariantSelector(variantRect, args, data);
            
                rowRect.height -= DefaultRowHeight + SpaceBetweenRowEditor * 2;
                rowRect.y += DefaultRowHeight + SpaceBetweenRowEditor * 2;

                if (rowRect.height > EditorGUIUtility.singleLineHeight)
                {
                    float sideSpace = 5;
                    float lift = SpaceBetweenRowEditor * 1.5f;
                    Rect separatorRect = new Rect(rowRect.x + sideSpace, rowRect.y - lift, rowRect.width - sideSpace * 2, 1);
                    EditorGUI.DrawRect(separatorRect, Color.gray);
                }
            }

            EditorGUI.BeginChangeCheck();
            int index = item.Index;
            CyanTriggerPropertyEditor.DrawActionInstanceInputEditors(
                data, 
                type => GetVariableOptions(type, index), 
                rowRect, 
                false);

            // TODO try to minimize the number of times this gets called...
            if (EditorGUI.EndChangeCheck() || data.NeedsRedraws || isUndo)
            {
                item.displayName = GetElementDisplayName(data.Property);
            }

            if (data.NeedsRedraws)
            {
                data.NeedsRedraws = false;
                _delayRefreshRowHeight = true;
            }
        }

        private void DrawVariantSelector(Rect rect, RowGUIArgs args, ActionData data)
        {
            int variantCount = CyanTriggerActionGroupDefinitionUtil.GetActionVariantCount(data.ActionInfo);
            
            float spaceBetween = 5;
            float width = (rect.width - spaceBetween * 2) / 3f;

            Rect labelRect = new Rect(rect.x, rect.y, width, rect.height);
            GUI.Label(labelRect, new GUIContent($"Action Variants ({variantCount})"));
            Rect buttonRect = new Rect(labelRect.xMax + spaceBetween, rect.y, rect.width - spaceBetween - labelRect.width, rect.height);
            
            EditorGUI.BeginDisabledGroup(variantCount <= 1);
            if (GUI.Button(buttonRect, data.ActionInfo.GetMethodSignature(), new GUIStyle(EditorStyles.popup)))
            {
                GenericMenu menu = new GenericMenu();
                
                foreach (var actionVariant in CyanTriggerActionGroupDefinitionUtil.GetActionVariantInfoHolders(data.ActionInfo))
                {
                    menu.AddItem(new GUIContent(actionVariant.GetMethodSignature()), false, (t) =>
                    {
                        var actionInfo = (CyanTriggerActionInfoHolder) t;
                        if (actionInfo == data.ActionInfo)
                        {
                            return;
                        }
                        
                        actionInfo.SetActionData(ItemElements[GetItemIndex(args.item.id)]);
                        data = GetOrCreateExpandData(args.item.id, true);
                        data.IsExpanded = true;
                        
                        OnActionChanged?.Invoke();
                        _delayRefreshRowHeight = true;
                    }, actionVariant);
                }
                
                menu.ShowAsContext();
            }
            EditorGUI.EndDisabledGroup();
        }
        
        protected override bool CanDuplicate(IEnumerable<int> items)
        {
            return true;
        }

        protected override List<int> DuplicateItems(IEnumerable<int> items)
        {
            List<int> newIds = new List<int>();
            HashSet<int> duplicatedInd = new HashSet<int>();
            List<int> sortedItems = new List<int>(items);
            sortedItems.Sort();

            Dictionary<string, string> variableGuidMap = new Dictionary<string, string>();
            
            foreach (int id in sortedItems)
            {
                int index = GetItemIndex(id);
                if (duplicatedInd.Contains(index))
                {
                    continue;
                }
                
                var item = Items[index];
                for (int i = item.Index; i <= item.ScopeEndIndex; ++i)
                {
                    DuplicateAction(ItemElements[i], variableGuidMap);
                    newIds.Add(Elements.arraySize - 1 + IdStartIndex);
                    duplicatedInd.Add(i);
                }
            }

            return newIds;
        }

        protected override bool AllowRenameOption()
        {
            return false;
        }

        protected override void GetRightClickMenuOptions(GenericMenu menu, Event currentEvent)
        {
            base.GetRightClickMenuOptions(menu, currentEvent);
            menu.AddSeparator("");
            
            // TODO add new actions at the parent of the selected
            menu.AddItem(new GUIContent("Add Local Variable"), false, () =>
            {
                CyanTriggerSearchWindowManager.Instance.DisplayVariableSearchWindow(
                    GUIUtility.GUIToScreenPoint(currentEvent.mousePosition), AddNewAction);
            });
            menu.AddItem(new GUIContent("Add Favorite Action"), false, () =>
            {
                CyanTriggerSearchWindowManager.Instance.DisplayActionFavoritesSearchWindow(
                    GUIUtility.GUIToScreenPoint(currentEvent.mousePosition), AddNewAction);
            });
            menu.AddItem(new GUIContent("Add Action"), false, () =>
            {
                CyanTriggerSearchWindowManager.Instance.DisplayActionSearchWindow(
                    GUIUtility.GUIToScreenPoint(currentEvent.mousePosition), AddNewActionDirect);
            });
        }
        
        protected override void DoubleClickedItem(int id)
        {
            var data = GetData(id);
            if (data == null || !ItemCanExpand(data))
            {
                return;
            }

            data.IsExpanded = !data.IsExpanded;
            _delayRefreshRowHeight = true;
        }
        
        protected override bool CanItemBeRemoved(CyanTriggerScopedTreeItem item)
        {
            return !IsItemHidden(item);
        }
        
        protected override bool CanItemBeMoved(CyanTriggerScopedTreeItem item)
        {
            return !IsItemHidden(item);
        }

        // TODO create a better method for this instead of implicitly using hidden
        private bool IsItemHidden(CyanTriggerScopedTreeItem item)
        {
            var data = GetOrCreateExpandData(item.id);
            var definition = data.ActionInfo.definition;
            return definition != null &&
                   CyanTriggerNodeDefinitionManager.DefinitionIsHidden(definition.fullName);
        }


        private void AddLocalVariable()
        {
            CyanTriggerSearchWindowManager.Instance.DisplayVariableSearchWindow(AddNewAction);
        }
        
        private void AddNewActionFromAllList()
        {
            CyanTriggerSearchWindowManager.Instance.DisplayActionSearchWindow(AddNewActionDirect);
        }

        private void AddNewActionFromFavoriteList()
        {
            CyanTriggerSearchWindowManager.Instance.DisplayActionFavoritesSearchWindow(AddNewAction);
        }
        
        private void AddNewActionFromSDK2List()
        {
            CyanTriggerSearchWindowManager.Instance.DisplaySDK2ActionFavoritesSearchWindow(AddNewAction);
        }

        private void AddNewAction(UdonNodeDefinition udonNode)
        {
            AddNewAction(CyanTriggerActionInfoHolder.GetActionInfoHolder(udonNode));
        }
        
        private void AddNewAction(CyanTriggerSettingsFavoriteItem favorite)
        {
            AddNewAction(CyanTriggerActionInfoHolder.GetActionInfoHolder(favorite));
        }

        private void AddNewActionDirect(CyanTriggerActionInfoHolder actionInfoHolder)
        {
            AddNewAction(actionInfoHolder);
        }
        
        private List<SerializedProperty> AddNewAction(
            CyanTriggerActionInfoHolder actionInfoHolder, 
            bool includeDependencies = true)
        {
            int startIndex = Elements.arraySize;
            var newProperties = actionInfoHolder.AddActionToEndOfPropertyList(Elements, includeDependencies);
            OnActionChanged?.Invoke();

            for (int i = startIndex; i < Elements.arraySize; ++i)
            {
                int id = i + IdStartIndex;
                SetExpanded(id, true);

                if (includeDependencies)
                {
                    GetOrCreateExpandData(id).IsExpanded = true;
                }
            }

            return newProperties;
        }

        private SerializedProperty DuplicateAction(
            SerializedProperty actionProperty,
            Dictionary<string, string> variableGuidMap)
        {
            var actionInfo = CyanTriggerActionInfoHolder.GetActionInfoHolderFromProperties(actionProperty);
            var dupedPropertyList = AddNewAction(actionInfo, false);
            Debug.Assert(dupedPropertyList.Count == 1,
                "Duplicating a property returned unexpected size! " + dupedPropertyList.Count);
            var dupedProperty = dupedPropertyList[0];
            
            actionInfo.CopyDataAndRemapVariables(actionProperty, dupedProperty, variableGuidMap);

            return dupedProperty;
        }
        
        protected override void OnItemsRemoved()
        {
            base.OnItemsRemoved();
            OnActionChanged?.Invoke();
        }

        protected override void OnElementsRemapped(int[] mapping, int prevIdStart)
        {
            base.OnElementsRemapped(mapping, prevIdStart);
            OnActionChanged?.Invoke();
        }

        protected override void OnElementRemapped(ActionData element, int prevIndex, int newIndex)
        {
            ClearInputList(element);
        }

        private void ClearInputList(ActionData element)
        {
            element.ClearInputLists();
        }
    }
}
