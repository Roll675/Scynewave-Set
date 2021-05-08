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
    public class CyanTriggerVariableTreeView : CyanTriggerScopedDataTreeView<CyanTriggerVariableTreeView.VariableExpandData>
    {
        public class VariableExpandData
        {
            public bool IsExpanded;
            public ReorderableList List;
        }
        
        private const float DefaultRowHeight = 20;
        private const float SpaceBetweenRowEditor = 6;
        private const float SpaceBetweenRowEditorSides = 6;
        
        private readonly AnimBool _showVariables;
        private readonly Action _onVariableAddedOrRemoved;
        private readonly Func<string, string, string> _getUniqueVariableName;

        private bool _delayRefreshRowHeight = false;
        
        private static MultiColumnHeader CreateColumnHeader()
        {
            string[] columnHeaders = {"Name", "Type", "Value", "Sync"};
            MultiColumnHeaderState.Column[] columns = new MultiColumnHeaderState.Column[4];
            for (int cur = 0; cur < columns.Length; ++cur)
            {
                columns[cur] = new MultiColumnHeaderState.Column
                {
                    minWidth = 50f,
                    width = 100f, 
                    headerTextAlignment = TextAlignment.Center, 
                    canSort = false,
                    headerContent = new GUIContent(columnHeaders[cur]),
                };
            }
            
            MultiColumnHeader multiColumnHeader = new MultiColumnHeader(new MultiColumnHeaderState(columns))
            {
                height = 18,
            };
            multiColumnHeader.ResizeToFit();
            
            return multiColumnHeader;
        }
        
        private static string GetElementDisplayName(SerializedProperty property)
        {
            return property.FindPropertyRelative(nameof(CyanTriggerVariable.name)).stringValue;
        }
        
        private static int GetElementScopeDelta(SerializedProperty property)
        {
            return 0;
        }

        public CyanTriggerVariableTreeView(
            SerializedProperty elements, 
            Action onVariableAddedOrRemoved,
            Func<string, string, string> getUniqueVariableName) 
            : base (elements, CreateColumnHeader(), GetElementScopeDelta, GetElementDisplayName)
        {
            showBorder = true;
            rowHeight = DefaultRowHeight;
            showAlternatingRowBackgrounds = true;
            useScrollView = false;
            _showVariables = new AnimBool(true);
            _onVariableAddedOrRemoved = onVariableAddedOrRemoved;
            _getUniqueVariableName = getUniqueVariableName;
            _showVariables.valueChanged.AddListener(Repaint);
            
            Reload();
        }

        private VariableExpandData GetOrCreateExpandData(int id)
        {
            var data = GetData(id);
            if (data == null)
            {
                data = new VariableExpandData();
                SetData(id, data);
            }

            return data;
        }

        public void DoLayoutTree()
        {
            bool showView = _showVariables.target;
            
            // TODO allow dragging objects/components here to add them as variables
            CyanTriggerPropertyEditor.DrawFoldoutListHeader(
                new GUIContent("Variables"),
                ref showView,
                false,
                Elements.arraySize,
                null,
                false,
                null,
                false,
                false
                );
            _showVariables.target = showView;
            
            if (!EditorGUILayout.BeginFadeGroup(_showVariables.faded))
            {
                EditorGUILayout.EndFadeGroup();
                return;
            }
            
            if (Size != Elements.arraySize)
            {
                _onVariableAddedOrRemoved?.Invoke();
                Reload();
            }
            
            Rect treeRect = EditorGUILayout.BeginVertical();
            treeRect.height = totalHeight + (Size == 0 ? DefaultRowHeight : 0);
            treeRect.x += 1;
            treeRect.width -= 2;
            GUILayout.Space(treeRect.height + 1);
            
            var listActionFooterIcons = new[]
            {
                EditorGUIUtility.TrIconContent("Favorite", "Choose to add to list"), //CustomSorting
                EditorGUIUtility.TrIconContent("Toolbar Plus More", "Choose to add to list"),
                EditorGUIUtility.TrIconContent("TreeEditor.Duplicate", "Duplicate selected item"),
                EditorGUIUtility.TrIconContent("Toolbar Minus", "Remove selection from list")
            };
            
            bool hasSelection = HasSelection();
            CyanTriggerPropertyEditor.DrawButtonFooter(
                listActionFooterIcons, new Action[]
                {
                    AddNewVariableFromFavoriteList,
                    AddNewVariableFromAllList,
                    DuplicateSelectedItems,
                    RemoveSelected
                },
                new []
                {
                    false, false, !hasSelection, !hasSelection
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
            
            SerializedProperty variableProperty = Elements.GetArrayElementAtIndex(scopedItem.Index);
            SerializedProperty typeProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.type));
            SerializedProperty typeDefProperty =
                typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
            Type type = Type.GetType(typeDefProperty.stringValue);

            if (CyanTriggerPropertyEditor.TypeHasSingleLineEditor(type))
            {
                return height;
            }

            VariableExpandData expandData = GetOrCreateExpandData(item.id);
            if (!expandData.IsExpanded)
            {
                return height;
            }
            
            // Calculate multi line height for the property
            SerializedProperty dataProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.data));
            var data = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);
            
            height = height + SpaceBetweenRowEditor * 2 + 
                   CyanTriggerPropertyEditor.HeightForEditor(type, data, true, ref expandData.List);

            return height;
        }

        protected override bool CanRename(TreeViewItem item)
        {
            return true;
        }
        
        protected override void RenameEnded(RenameEndedArgs args)
        {
            if (!args.acceptedRename || args.newName.Equals(args.originalName))
            {
                return;
            }
            
            int index = GetItemIndex(args.itemID);
            var variableProperty = Elements.GetArrayElementAtIndex(index);
            
            var guid = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.variableID)).stringValue;
            string newName = _getUniqueVariableName(args.newName, guid);
            
            if (args.newName.Equals(args.originalName))
            {
                return;
            }
            
            variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.name)).stringValue = newName;
            Items[index].displayName = newName;
            _onVariableAddedOrRemoved?.Invoke();
        }

        protected override bool CanDuplicate(IEnumerable<int> items)
        {
            return true;
        }

        protected override List<int> DuplicateItems(IEnumerable<int> items)
        {
            List<int> newIds = new List<int>();
            foreach (int id in GetSelection())
            {
                int index = GetItemIndex(id);
                DuplicateVariable(Elements.GetArrayElementAtIndex(index));
                newIds.Add(id + IdStartIndex);
            }

            return newIds;
        }

        protected override void GetRightClickMenuOptions(GenericMenu menu, Event currentEvent)
        {
            base.GetRightClickMenuOptions(menu, currentEvent);
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Add Variable"), false, () =>
            {
                CyanTriggerSearchWindowManager.Instance.DisplayVariableSearchWindow(
                    GUIUtility.GUIToScreenPoint(currentEvent.mousePosition), AddNewVariable);
            });
            menu.AddItem(new GUIContent("Add Favorite Variable"), false, () =>
            {
                CyanTriggerSearchWindowManager.Instance.DisplayVariableFavoritesSearchWindow(
                    GUIUtility.GUIToScreenPoint(currentEvent.mousePosition), AddNewVariable);
            });
        }

        protected override void DoubleClickedItem(int id)
        {
            var data = GetData(id);
            if (data == null)
            {
                return;
            }

            data.IsExpanded = !data.IsExpanded;
            _delayRefreshRowHeight = true;
        }

        protected override void OnItemsRemoved()
        {
            _onVariableAddedOrRemoved?.Invoke();
        }

        protected override void RowGUI (RowGUIArgs args)
        {
            var item = (CyanTriggerScopedTreeItem) args.item;
            
            // Only draw variable fields when not renaming the variable
            if (!args.isRenaming)
            {
                SerializedProperty variableProperty = Elements.GetArrayElementAtIndex(item.Index);
                SerializedProperty typeProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.type));
                SerializedProperty typeDefProperty =
                    typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
                Type type = Type.GetType(typeDefProperty.stringValue);

                for (int i = 0; i < args.GetNumVisibleColumns(); ++i)
                {
                    Rect cellRect = args.GetCellRect(i);
                    cellRect.height = DefaultRowHeight;
                    CellGUI(cellRect, args.GetColumn(i), item, variableProperty, type);
                }
            }

            Rect editorRect = new Rect(args.rowRect);
            editorRect.y += DefaultRowHeight + SpaceBetweenRowEditor;
            editorRect.height -= DefaultRowHeight + SpaceBetweenRowEditor * 2;
            editorRect.x += SpaceBetweenRowEditorSides;
            editorRect.width -= SpaceBetweenRowEditorSides * 2;
            DrawMultilineVariableEditor(item.Index, editorRect);
        }

        void CellGUI(Rect cellRect, int column, CyanTriggerScopedTreeItem item, SerializedProperty variableProperty, Type type)
        {
            CenterRectUsingSingleLineHeight(ref cellRect);
            
            switch (column)
            {
                case 0: // Name
                {
                    GUIContent content = new GUIContent(item.displayName, item.displayName);
                    CyanTriggerNameHelpers.TruncateContent(content, cellRect);
                    EditorGUI.LabelField(cellRect, content);
                    //args.rowRect = cellRect;
                    //base.RowGUI(args);
                    break;
                }
                case 1: // Type
                {
                    string typeName = CyanTriggerNameHelpers.GetTypeFriendlyName(type);
                    GUIContent content = new GUIContent(typeName, typeName);
                    CyanTriggerNameHelpers.TruncateContent(content, cellRect);
                    EditorGUI.LabelField(cellRect, content);
                    break;
                }
                case 2: // Value
                {
                    SerializedProperty dataProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.data));
                    
                    if (!CyanTriggerPropertyEditor.TypeHasSingleLineEditor(type))
                    {
                        VariableExpandData expandData = GetOrCreateExpandData(item.id);
                        if (GUI.Button(cellRect, new GUIContent(expandData.IsExpanded ? "Hide" : "Edit")))
                        {
                            expandData.IsExpanded = !expandData.IsExpanded;
                            _delayRefreshRowHeight = true;
                        }
                    }
                    else
                    {
                        CyanTriggerPropertyEditor.DrawEditor(dataProperty, cellRect, GUIContent.none, type, false);
                    }
                    break;
                }
                case 3: // Sync
                {
                    SerializedProperty syncProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.sync));
                    
                    // TODO verify what items can and can't be synced.
                    // TODO force set to not synced if is not a value type
                    EditorGUI.BeginDisabledGroup(!type.IsValueType || type == typeof(ParticleSystem.MinMaxCurve));
                    EditorGUI.PropertyField(cellRect, syncProperty, GUIContent.none);
                    EditorGUI.EndDisabledGroup();
                    
                    break;
                }
            }
        }
        
        private void DrawMultilineVariableEditor(int index, Rect rect)
        {
            SerializedProperty variableProperty = Elements.GetArrayElementAtIndex(index);
            SerializedProperty typeProperty =
                variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.type));
            SerializedProperty typeDefProperty =
                typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
            Type type = Type.GetType(typeDefProperty.stringValue);

            int id = Items[index].id;
            VariableExpandData expandData = GetOrCreateExpandData(id);
            
            if (!CyanTriggerPropertyEditor.TypeHasSingleLineEditor(type) && expandData.IsExpanded)
            {
                SerializedProperty nameProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.name));
                SerializedProperty dataProperty =
                    variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.data));
                
                if (type.IsArray)
                {
                    int size = expandData.List == null ? 0 : expandData.List.count;
                    bool showArray = expandData.IsExpanded;
                    
                    string display = CyanTriggerNameHelpers.GetTypeFriendlyName(type) + " " + nameProperty.stringValue;
                    CyanTriggerPropertyEditor.DrawArrayEditor(
                        dataProperty,
                        new GUIContent(display),
                        type,
                        ref expandData.IsExpanded,
                        ref expandData.List,
                        false,
                        rect);

                    int newSize = expandData.List == null ? 0 : expandData.List.count;
                    if (size != newSize || showArray != expandData.IsExpanded)
                    {
                        _delayRefreshRowHeight = true;
                    }
                }
                else
                {
                    CyanTriggerPropertyEditor.DrawEditor(dataProperty, rect, GUIContent.none, type, false);
                }
            }
        }

        private void AddNewVariableFromAllList()
        {
            CyanTriggerSearchWindowManager.Instance.DisplayVariableSearchWindow(AddNewVariable);
        }

        private void AddNewVariableFromFavoriteList()
        {
            CyanTriggerSearchWindowManager.Instance.DisplayVariableFavoritesSearchWindow(AddNewVariable);
        }
        
        private void AddNewVariable(UdonNodeDefinition def)
        {
            AddNewVariable(CyanTriggerNameHelpers.GetTypeFriendlyName(def.type), def.type);
        }

        private void AddNewVariable(CyanTriggerSettingsFavoriteItem favorite)
        {
            if (string.IsNullOrEmpty(favorite.data.directEvent))
            {
                Debug.LogWarning("Cannot create a new variable without a proper definition!");
                return;
            }

            var def = CyanTriggerNodeDefinitionManager.GetDefinition(favorite.data.directEvent);
            if (def == null)
            {
                Debug.LogWarning("Cannot create a new variable without a proper definition!");
                return;
            }

            AddNewVariable(CyanTriggerNameHelpers.GetTypeFriendlyName(def.baseType), def.baseType);
        }

        private void AddNewVariable(string variableName, Type type, object data = default, bool rename = true)
        {
            Elements.arraySize++;
            SerializedProperty newVariableProperty =
                Elements.GetArrayElementAtIndex(Elements.arraySize - 1);

            SerializedProperty idProperty =
                newVariableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.variableID));
            idProperty.stringValue = Guid.NewGuid().ToString();

            SerializedProperty nameProperty = newVariableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.name));
            nameProperty.stringValue = _getUniqueVariableName(variableName, idProperty.stringValue);

            SerializedProperty syncProperty = newVariableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.sync));
            syncProperty.enumValueIndex = (int) CyanTriggerSyncMode.NotSynced;

            SerializedProperty typeProperty = newVariableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.type));
            SerializedProperty typeDefProperty =
                typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
            typeDefProperty.stringValue = type.AssemblyQualifiedName;

            SerializedProperty dataProperty = newVariableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.data));

            if (data == null)
            {
                CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty,
                    type.IsValueType ? Activator.CreateInstance(type) : null);
            }
            else
            {
                CyanTriggerSerializableObject.UpdateSerializedProperty(dataProperty, data);
            }

            _onVariableAddedOrRemoved?.Invoke();

            if (rename)
            {
                Reload();
                BeginRename(Items[Elements.arraySize - 1]);
            }
        }

        private void DuplicateVariable(SerializedProperty variableProperty)
        {
            SerializedProperty nameProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.name));
            SerializedProperty typeProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.type));
            SerializedProperty typeDefProperty =
                typeProperty.FindPropertyRelative(nameof(CyanTriggerSerializableType.typeDef));
            Type type = Type.GetType(typeDefProperty.stringValue);
            SerializedProperty dataProperty = variableProperty.FindPropertyRelative(nameof(CyanTriggerVariable.data));
            var data = CyanTriggerSerializableObject.ObjectFromSerializedProperty(dataProperty);
            
            AddNewVariable(nameProperty.stringValue, type, data, false);
        }
    }
}
