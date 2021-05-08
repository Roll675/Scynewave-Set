using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CyanTrigger
{
    public class CyanTriggerScopedTreeItem : TreeViewItem
    {
        public int Index;
        public bool HasScope;
        public int ScopeEndIndex;

        public CyanTriggerScopedTreeItem() {}
        public CyanTriggerScopedTreeItem(int id, int depth, string name) : base(id, depth, name) { }
    }
    
    public abstract class CyanTriggerScopedTreeView : TreeView
    {
        private const string kDragAndDropDataKey = "ScopedTreeViewDragging";
        private const string kDragAndDropObjectDataKey = "ScopedTreeViewDraggingObject";

        private SerializedProperty _elements;
        public SerializedProperty Elements
        {
            get => _elements;
            set
            {
                _elements = value;
                Reload();
            }
        }

        protected CyanTriggerScopedTreeItem[] Items;
        protected SerializedProperty[] ItemElements;
        private readonly Func<SerializedProperty, int> _getElementScopeDelta;
        private readonly Func<SerializedProperty, string> _getElementDisplayName;
        
        public int Size { get; private set; }
        public int VisualSize { get; private set; }

        private int _idStartIndex;
        public int IdStartIndex
        {
            get => _idStartIndex;
            set
            {
                if (value == _idStartIndex)
                {
                    return;
                }

                int prev = _idStartIndex;
                _idStartIndex = value;
                OnIdStartIndexChanged(prev, value);
                Reload();
            }
        }

        protected void OnIdStartIndexChanged(int prev, int cur)
        {
            UpdateExpandedAndSelection(prev);
        }

        protected CyanTriggerScopedTreeView(
            SerializedProperty elements, 
            MultiColumnHeader header, 
            Func<SerializedProperty, int> getElementScopeDelta,
            Func<SerializedProperty, string> getElementDisplayName) : base (new TreeViewState())
        {
            _elements = elements;
            multiColumnHeader = header;
            _getElementScopeDelta = getElementScopeDelta;
            _getElementDisplayName = getElementDisplayName;
            
            // Get TreeViewController and allow deselection on clicking nothing
            FieldInfo info = typeof(TreeView).GetField("m_TreeView", BindingFlags.NonPublic | BindingFlags.Instance);
            if (info != null)
            {
                var item = info.GetValue(this);
                PropertyInfo deselectInfo = item.GetType().GetProperty("deselectOnUnhandledMouseDown");
                if (deselectInfo != null)
                {
                    deselectInfo.SetValue(item, true);
                }
            }
            
            Reload();
        }

        public int GetItemIndex(TreeViewItem item)
        {
            return ((CyanTriggerScopedTreeItem) item).Index;
        }
        
        public int GetItemIndex(int id)
        {
            return id - IdStartIndex;
        }

        public CyanTriggerScopedTreeItem GetItem(int index)
        {
            return Items[index];
        }

        protected override bool CanBeParent(TreeViewItem item)
        {
            CyanTriggerScopedTreeItem customItem = (CyanTriggerScopedTreeItem) item;
            return customItem.HasScope;
        }

        // Override to reject for other reasons. 
        protected virtual bool ShouldRejectDragAndDrop(DragAndDropArgs args)
        {
            return false;
        }
        
        protected virtual bool CanItemBeRemoved(CyanTriggerScopedTreeItem item)
        {
            return true;
        }
        
        protected virtual bool CanItemBeMoved(CyanTriggerScopedTreeItem item)
        {
            return true;
        }

        protected override DragAndDropVisualMode HandleDragAndDrop(DragAndDropArgs args)
        {
            // TODO provide interface for dragging to other tree items.
            if (DragAndDrop.GetGenericData(kDragAndDropObjectDataKey) != this)
            {
                return DragAndDropVisualMode.None;
            }
            
            if (!(DragAndDrop.GetGenericData(kDragAndDropDataKey) is List<int> draggedIds))
            {
                return DragAndDropVisualMode.None;
            }
            
            CyanTriggerScopedTreeItem parent = (CyanTriggerScopedTreeItem)args.parentItem;
            if (parent == null)
            {
                parent = (CyanTriggerScopedTreeItem)rootItem;
            }

            // Reject movement where a parent would be put underneath itself
            {
                CyanTriggerScopedTreeItem temp = parent;
                while (temp != null)
                {
                    if (IsSelected(temp.id))
                    {
                        return DragAndDropVisualMode.Rejected;
                    }

                    temp = (CyanTriggerScopedTreeItem)temp.parent;
                }
            }

            if (ShouldRejectDragAndDrop(args))
            {
                return DragAndDropVisualMode.Rejected;
            }

            if (args.performDrop)
            {
                MoveElements(draggedIds, parent, args.dragAndDropPosition, args.insertAtIndex);
            }
            return DragAndDropVisualMode.Move;
        }

        protected void MoveElements(
            List<int> movedIds, 
            CyanTriggerScopedTreeItem parent, 
            DragAndDropPosition dragPosition, 
            int insertPosition)
        {
            List<int> movedItems = new List<int>();
            foreach (int id in movedIds)
            {
                int index = GetItemIndex(id);
                if (CanItemBeMoved(Items[index]))
                {
                    movedItems.Add(id);
                }
            }
            movedItems.Sort();
                
            MoveElementProperties(new List<int>(movedItems), parent, dragPosition, insertPosition);
            MoveTreeNodes(movedItems, parent, dragPosition, insertPosition);
            UpdateExpandedAndSelection();
            _elements.serializedObject.ApplyModifiedProperties();
            Reload();
        }
        
        private void MoveElementProperties(
            List<int> movedIds, 
            CyanTriggerScopedTreeItem parent,
            DragAndDropPosition dragPosition, 
            int insertPosition)
        {
            movedIds.Reverse();
            
            int insertIndex = 0;
            if (dragPosition == DragAndDropPosition.UponItem)
            {
                insertIndex = parent.ScopeEndIndex;
            }
            else
            {
                int child = insertPosition - 1;
                if (child < 0)
                {
                    insertIndex = parent.Index + 1;
                }
                else
                {
                    insertIndex = parent.children == null
                        ? parent.ScopeEndIndex
                        : ((CyanTriggerScopedTreeItem) parent.children[child]).ScopeEndIndex + 1;
                }
            }
            
            int movedItems = 0;
            int origInsert = insertIndex;
                
            foreach (int id in movedIds)
            {
                int index = GetItemIndex(id);
                CyanTriggerScopedTreeItem item = Items[index];
                    
                int idIndex = index;
                if (idIndex > origInsert)
                {
                    idIndex += movedItems;
                }
                
                int totalMoved = item.ScopeEndIndex - index + 1;
                    
                for (int child = 0; child < totalMoved; ++child)
                {
                    int from = idIndex;
                    int to = insertIndex;

                    if (idIndex > insertIndex)
                    {
                        from += child;
                        to += child;
                    }
                    else
                    {
                        --to;
                    }
                    
                    Elements.MoveArrayElement(from, to);
                }

                ((CyanTriggerScopedTreeItem) item.parent).ScopeEndIndex -= totalMoved;

                if (idIndex < insertIndex)
                {
                    insertIndex -= totalMoved;
                }
                movedItems += totalMoved;
            }
        }

        private void MoveTreeNodes(
            List<int> movedIds,
            CyanTriggerScopedTreeItem parent,
            DragAndDropPosition dragPosition, 
            int insertPosition)
        {
            if (!parent.hasChildren)
            {
                parent.children = new List<TreeViewItem>();
            }
            
            int insertIndex = insertPosition;
            if (dragPosition == DragAndDropPosition.UponItem)
            {
                insertIndex = parent.children.Count;
            }
            
            foreach (int id in movedIds)
            {
                int index = GetItemIndex(id);
                var node = Items[index];
                var prevParent = node.parent;
                if (prevParent == parent)
                {
                    int nodeIndex = prevParent.children.IndexOf(node);
                    if (nodeIndex < insertIndex)
                    {
                        --insertIndex;
                    }
                }
                node.parent.children.Remove(node);
                parent.children.Insert(insertIndex, node);
                ++insertIndex;
            }
        }

        private void RemapNodeIds(CyanTriggerScopedTreeItem node, int[] mapping, ref int id)
        {
            if (node != rootItem)
            {
                mapping[node.Index] = IdStartIndex + id;
                node.id = IdStartIndex + id;
                node.Index = id;
                ++id;
            }

            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    RemapNodeIds((CyanTriggerScopedTreeItem)child, mapping, ref id);
                }
            }

            if (node.HasScope)
            {
                ++id;
            }
        }

        private void UpdateExpandedAndSelection(int prevIdStart = -1)
        {
            if (prevIdStart == -1)
            {
                prevIdStart = IdStartIndex;
            }
            
            int[] mapping = new int[Elements.arraySize];
            for (int i = 0; i < mapping.Length; ++i)
            {
                mapping[i] = -1;
            }
            int idRef = 0;
            RemapNodeIds((CyanTriggerScopedTreeItem)rootItem, mapping, ref idRef);

            List<int> selection = new List<int>();
            List<int> expanded = new List<int>();

            foreach (var id in GetSelection())
            {
                int index = id - prevIdStart;
                if (id != -1 && mapping[index] != -1)
                {
                    selection.Add(mapping[index]);
                }
            }
            foreach (var id in GetExpanded())
            {
                int index = id - prevIdStart;
                if (id != -1 && mapping[index] != -1)
                {
                    expanded.Add(mapping[index]);    
                }
            }
            SetSelection(selection, TreeViewSelectionOptions.FireSelectionChanged);
            SetExpanded(expanded);
            OnElementsRemapped(mapping, prevIdStart);
        }

        protected virtual void OnElementsRemapped(int[] mapping, int prevIdStart) { }

        // Remove selected and child elements
        public void RemoveSelected()
        {
            List<int> selected = new List<int>();
            foreach (int id in GetSelection())
            {
                int index = GetItemIndex(id);
                if (CanItemBeRemoved(Items[index]))
                {
                    selected.Add(id);
                }
            }
            selected.Sort();
            selected.Reverse();

            // Update node list first so that element size contains everything
            foreach (var id in selected)
            {
                int index = GetItemIndex(id);
                var item = Items[index];
                var parent = item.parent;
                parent.children.Remove(item);
            }

            // Selection is cleared as selection was removed from the tree
            SetSelection(new List<int>(), TreeViewSelectionOptions.FireSelectionChanged);
            UpdateExpandedAndSelection();

            // update serialized properties to remove the node and everything between it and the end scope node.
            foreach (int id in selected)
            {
                int index = GetItemIndex(id);
                var item = Items[index];
                if (item == null)
                {
                    continue;
                }

                Elements.DeleteArrayElementAtIndex(index);
                
                if (item.ScopeEndIndex != index)
                {
                    int scope = 1;

                    while (index < Elements.arraySize)
                    {
                        int scopeDelta = _getElementScopeDelta(Elements.GetArrayElementAtIndex(index));
                        Elements.DeleteArrayElementAtIndex(index);
                        scope += scopeDelta;
                        if (scope == 0)
                        {
                            break;
                        }
                    }
                }
            }

            OnItemsRemoved();
            _elements.serializedObject.ApplyModifiedProperties();
            Reload();
        }
        
        protected virtual void OnItemsRemoved() { }
        
        protected override bool CanStartDrag(CanStartDragArgs args) => true;
        
        protected override void SetupDragAndDrop(SetupDragAndDropArgs args)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(kDragAndDropDataKey, args.draggedItemIDs);
            DragAndDrop.SetGenericData(kDragAndDropObjectDataKey, this);
            DragAndDrop.objectReferences = new Object[0];
            DragAndDrop.StartDrag("Drag Elements");
        }

        protected override TreeViewItem BuildRoot()
        {
            int arraySize = Elements.arraySize;
            Size = arraySize;
            VisualSize = 0;
            Stack<CyanTriggerScopedTreeItem> parents = new Stack<CyanTriggerScopedTreeItem>();
            
            CyanTriggerScopedTreeItem root = new CyanTriggerScopedTreeItem()
            {
                id = -1,
                depth = -1,
                displayName = "Root",
                Index = -1,
            };
            parents.Push(root);
            
            Items = new CyanTriggerScopedTreeItem[Size];
            ItemElements = new SerializedProperty[Size];
            
            List<TreeViewItem> treeViewItemList = new List<TreeViewItem>(arraySize);
            for (int id = 0; id < arraySize; ++id)
            {
                var property = Elements.GetArrayElementAtIndex(id);
                ItemElements[id] = property;
                string name = _getElementDisplayName(property);
                int scopeDelta = _getElementScopeDelta(property);
                
                if (scopeDelta < 0)
                {
                    var lastParent = parents.Pop();
                    lastParent.ScopeEndIndex = id;
                    continue;
                }

                ++VisualSize;

                CyanTriggerScopedTreeItem treeViewItem =
                    new CyanTriggerScopedTreeItem(id + IdStartIndex, parents.Count - 1, name);
                treeViewItemList.Add(treeViewItem);
                treeViewItem.Index = id;
                treeViewItem.ScopeEndIndex = id;
                treeViewItem.HasScope = scopeDelta != 0;
                Items[id] = treeViewItem;
                
                if (scopeDelta > 0)
                {
                    parents.Push(treeViewItem);
                }
            }
            SetupParentsAndChildrenFromDepths(root, treeViewItemList);
            OnBuildRoot(root);
            return root;
        }
        
        public override void OnGUI(Rect rect)
        {
            if (Size != Elements.arraySize)
            {
                Reload();
            }
            base.OnGUI(rect);

            HandleRightClick(rect);
        }

        protected override void KeyEvent()
        {
            if (Event.current.type != EventType.KeyDown)
            {
                return;
            }
            
            KeyCode keyCode = Event.current.keyCode;
            switch (keyCode)
            {
                case KeyCode.Delete:
                {
                    Event.current.Use();
                    RemoveSelected();
                    break;
                }
                case KeyCode.D:
                {
                    if (Event.current.modifiers == EventModifiers.Control)
                    {
                        Event.current.Use();
                        if (CanDuplicate(GetSelection()))
                        {
                            DuplicateSelectedItems();
                        }
                    }
                    break;
                }
            }
        }

        protected virtual void OnBuildRoot(CyanTriggerScopedTreeItem root) { }

        protected virtual bool CanDuplicate(IEnumerable<int> items)
        {
            return false;
        }

        protected virtual List<int> DuplicateItems(IEnumerable<int> items)
        {
            throw new NotImplementedException();
        }

        protected virtual void DuplicateSelectedItems()
        {
            List<int> sortedItems = new List<int>(GetSelection());
            sortedItems.Sort();

            int parentId = -1;
            int minDepth = Int32.MaxValue;
            foreach (int id in sortedItems)
            {
                int index = GetItemIndex(id);
                var item = Items[index];
                if (item.depth < minDepth)
                {
                    minDepth = item.depth;
                    parentId = item.parent.id;
                }
            }

            var dupedIds = DuplicateItems(sortedItems);
            SetSelection(new List<int>());
            Reload();

            List<int> draggedItems = new List<int>();
            foreach (int id in dupedIds)
            {
                int index = GetItemIndex(id);
                var item = Items[index];
                
                // Only find items at the root
                if (item != null && item.parent.id == -1)
                {
                    draggedItems.Add(id);
                }
            }
            SetSelection(draggedItems, TreeViewSelectionOptions.FireSelectionChanged);
            
            // Move items into nested position and not at the end of the root
            if (parentId != -1)
            {
                MoveElements(draggedItems, Items[GetItemIndex(parentId)], DragAndDropPosition.UponItem, 0);
            }
        }
        
        protected virtual bool ShowRightClickMenu()
        {
            return true;
        }

        protected virtual bool AllowRenameOption()
        {
            return true;
        }
        
        protected virtual void GetRightClickMenuOptions(GenericMenu menu, Event currentEvent)
        {
            IList<int> selection = GetSelection();
            
            // Rename
            if (AllowRenameOption())
            {
                bool canRenameItem = false;
                TreeViewItem renameItem = null;
                if (selection.Count == 1)
                {
                    int id = selection[0];
                    renameItem = Items[GetItemIndex(id)];
                    canRenameItem = CanRename(renameItem);
                }

                if (canRenameItem)
                {
                    menu.AddItem(new GUIContent("Rename"), false, () => BeginRename(renameItem));
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Rename"));
                }
            }
            
            menu.AddItem(new GUIContent("Deselect"), false, () =>
            {
                SetSelection(new int[0], TreeViewSelectionOptions.FireSelectionChanged);
            });
            
            if (CanDuplicate(selection))
            {
                menu.AddItem(new GUIContent("Duplicate"), false, DuplicateSelectedItems);
            }

            menu.AddItem(new GUIContent("Delete"), false, () =>
            {
                RemoveSelected();
                Elements.serializedObject.ApplyModifiedProperties();
            });
        }
        
        private void HandleRightClick(Rect rect)
        {
            if (!ShowRightClickMenu())
            {
                return;
            }
            
            Event current = Event.current;
            if(current.type == EventType.ContextClick && rect.Contains(current.mousePosition))
            {
                GenericMenu menu = new GenericMenu();
                GetRightClickMenuOptions(menu, current);
                
                menu.ShowAsContext();
 
                current.Use(); 
            }
        }
    }
}