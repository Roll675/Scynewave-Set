using System;
using System.Collections.Generic;
using UnityEditor.Experimental.UIElements.GraphView;
using UnityEngine;

namespace CyanTrigger
{
    public class CyanTriggerFocusedSearchWindow : ScriptableObject, ISearchWindowProvider
    {
        public string WindowTitle;
        public List<CyanTriggerActionInfoHolder> FocusedNodeDefinitions;
        public Action<CyanTriggerActionInfoHolder> OnDefinitionSelected;
        public Func<CyanTriggerActionInfoHolder, string> GetDisplayString;
        

        #region ISearchWindowProvider

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> nodeEntries = new List<SearchTreeEntry>();

            nodeEntries.Add(new SearchTreeGroupEntry(new GUIContent($"{WindowTitle} Search"), 0));

            HashSet<string> usedNames = new HashSet<string>();
            foreach (var infoHolder in FocusedNodeDefinitions)
            {
                string infoName = GetDisplayString(infoHolder);
                if (usedNames.Contains(infoName))
                {
                    continue;
                }
                usedNames.Add(infoName);
                
                nodeEntries.Add(new SearchTreeEntry(new GUIContent(infoName)) {level = 1, userData = infoHolder});    
            }

            return nodeEntries;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            if (entry.userData is CyanTriggerActionInfoHolder actionInfoHolder && OnDefinitionSelected != null)
            {
                // Debug to make setting up favorites easier...
                // foreach (var v in FocusedNodeDefinitions)
                // {
                //     OnDefinitionSelected.Invoke(v);
                // }
                OnDefinitionSelected.Invoke(actionInfoHolder);
                return true;
            }

            return false;
        }
        
        #endregion

        public void ResetDisplayMethod()
        {
            GetDisplayString = UseDisplayName;
        }

        public string UseDisplayName(CyanTriggerActionInfoHolder infoHolder)
        {
            return infoHolder.GetDisplayName();
        }
    }
}