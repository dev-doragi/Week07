using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TutorialDialogueData", menuName = "Data/Tutorial Dialogue Data")]
public class TutorialDialogueDataSO : ScriptableObject
{
    [Serializable]
    public class StepDialogue
    {
        [Min(0)] public int StepIndex;
        public string Title;
        [TextArea(2, 5)] public List<string> Lines = new List<string>();
    }

    [SerializeField] private List<StepDialogue> _entries = new List<StepDialogue>();

    public bool TryGetDialogue(int stepIndex, out StepDialogue dialogue)
    {
        for (int i = 0; i < _entries.Count; i++)
        {
            if (_entries[i] != null && _entries[i].StepIndex == stepIndex)
            {
                dialogue = _entries[i];
                return true;
            }
        }

        dialogue = null;
        return false;
    }
}
