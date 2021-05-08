
namespace CyanTrigger
{
    public static class CyanTriggerVersionMigrator
    {
        // Returns true if the trigger was migrated.
        public static bool MigrateTrigger(CyanTriggerDataInstance cyanTrigger)
        {
            if (cyanTrigger == null)
            {
                return false;
            }
            
            bool migrated = false;
            if (cyanTrigger.version == 0)
            {
                cyanTrigger.version = 1;
                migrated = true;
                MigrateTriggerToVersion1(cyanTrigger);
            }

            if (cyanTrigger.version == 1)
            {
                cyanTrigger.version = 2;
                migrated = true;
                MigrateTriggerToVersion2(cyanTrigger);
            }

            // TODO add more version migrations as data changes

            return migrated;
        }

        #region Version 2 Migration
        /*
         Version 2 changes
         - Renaming PassIfTrue and FailIfFalse with "Condition" prefix
         - Renaming "ActivateCustomTrigger" to "SendCustomEvent"
        */
        private static void MigrateTriggerToVersion2(CyanTriggerDataInstance cyanTrigger)
        {
            void MigrateTriggerActionData(CyanTriggerActionInstance actionInstance)
            {
                switch (actionInstance.actionType.directEvent)
                {
                    case "CyanTriggerSpecial_FailIfFalse":
                        actionInstance.actionType.directEvent = "CyanTriggerSpecial_ConditionFailIfFalse";
                        break;
                    case "CyanTriggerSpecial_PassIfTrue":
                        actionInstance.actionType.directEvent = "CyanTriggerSpecial_ConditionPassIfTrue";
                        break;
                    case "CyanTrigger.__ActivateCustomTrigger__CyanTrigger__SystemString":
                        actionInstance.actionType.directEvent = "CyanTrigger.__SendCustomEvent__CyanTrigger__SystemString";
                        break;
                }
            }
            
            foreach (var eventTrigger in cyanTrigger.events)
            {
                foreach (var actionInstance in eventTrigger.actionInstances)
                {
                    MigrateTriggerActionData(actionInstance);
                }
            }
        }

        #endregion
        
        #region Version 1 Migration
        
        /*
         Version 1 changes
         - "this" variables now start with an underscore
         - variable providers use variable id and name instead of two variable's data fields
        */
        private static void MigrateTriggerToVersion1(CyanTriggerDataInstance cyanTrigger)
        {
            void MigrateTriggerVariable(CyanTriggerActionVariableInstance variableInstance)
            {
                if (variableInstance.isVariable && variableInstance.variableID.StartsWith("this_"))
                {
                    variableInstance.variableID = "_" + variableInstance.variableID;
                }
            }
            
            void MigrateTriggerActionData(CyanTriggerActionInstance actionInstance)
            {
                foreach (var variable in actionInstance.multiInput)
                {
                    MigrateTriggerVariable(variable);
                }
                foreach (var variable in actionInstance.inputs)
                {
                    MigrateTriggerVariable(variable);
                }
            }
            
            foreach (var eventTrigger in cyanTrigger.events)
            {
                foreach (var actionInstance in eventTrigger.actionInstances)
                {
                    MigrateTriggerActionData(actionInstance);
                    
                    // Update variable providers so variables only take one input instead of two
                    if (CyanTriggerNodeDefinitionManager.TryGetCustomDefinition(actionInstance.actionType.directEvent,
                        out var customDefinition) && customDefinition is CyanTriggerCustomNodeVariableProvider variableProvider)
                    {
                        variableProvider.MigrateTriggerToVersion1(actionInstance);
                    }
                }
            }
        }

        #endregion
    }
}
