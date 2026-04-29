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

        /// <summary>
        /// null이면 SO의 DefaultPortraitSprite 사용
        /// 특정 스텝에만 다른 초상화를 표시하고 싶을 때만 지정
        /// </summary>
        public Sprite PortraitSpriteOverride;
    }

    [Header("Default Settings")]
    [SerializeField] private string _defaultSpeakerName = "Tutorial NPC";
    [SerializeField] private Sprite _defaultPortraitSprite;

    [Header("Dialogue Entries")]
    [SerializeField] private List<StepDialogue> _entries = new List<StepDialogue>();

    /// <summary>
    /// 스텝 인덱스로 대사 데이터 조회
    /// </summary>
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

    /// <summary>
    /// 스피커 이름 조회 (모든 스텝에서 동일)
    /// </summary>
    public string GetSpeakerName()
    {
        return _defaultSpeakerName;
    }

    /// <summary>
    /// 해당 스텝의 초상화 스프라이트 조회
    /// - PortraitSpriteOverride가 설정되어 있으면 그것 사용
    /// - 아니면 기본 스프라이트 사용
    /// </summary>
    public Sprite GetPortraitSprite(int stepIndex)
    {
        if (TryGetDialogue(stepIndex, out var dialogue) && dialogue.PortraitSpriteOverride != null)
        {
            return dialogue.PortraitSpriteOverride;
        }

        return _defaultPortraitSprite;
    }

    /// <summary>
    /// 기본 초상화 스프라이트 조회 (Override 없을 때)
    /// </summary>
    public Sprite GetDefaultPortraitSprite()
    {
        return _defaultPortraitSprite;
    }
}
