using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TutorialStepRunner
{
    private const float CAMERA_DEFAULT_POS_Z = -10f;

    private readonly TutorialDialoguePresenter _dialoguePresenter;
    private readonly System.Func<bool> _nextClickedChecker;
    private readonly bool _ignoreConditionsForPlaytest;
    private readonly MonoBehaviour _coroutineHost;
    private readonly RectTransform _highlighter;

    public TutorialStepRunner(
        TutorialDialoguePresenter dialoguePresenter,
        System.Func<bool> nextClickedChecker,
        bool ignoreConditionsForPlaytest,
        MonoBehaviour coroutineHost,
        RectTransform highlighter)
    {
        _dialoguePresenter = dialoguePresenter;
        _nextClickedChecker = nextClickedChecker;
        _ignoreConditionsForPlaytest = ignoreConditionsForPlaytest;
        _coroutineHost = coroutineHost;
        _highlighter = highlighter;
    }

    public IEnumerator RunStep(TutorialStep step, int stepIndex, int totalStepCount)
    {
        EventBus.Instance?.Publish(new TutorialStepStartedEvent
        {
            StepIndex = stepIndex,
            TotalStepCount = totalStepCount,
            StepData = step
        });

        var startupModules = BuildStartupModules(step, stepIndex);
        var completionModule = BuildCompletionModule(step);
        var advanceModule = BuildAdvanceModule(step);

        foreach (var module in startupModules)
        {
            module.Initialize(step);
        }

        completionModule?.Initialize(step);
        advanceModule?.Initialize(step);

        try
        {
            foreach (var module in startupModules)
            {
                yield return module.Execute();
            }

            if (completionModule != null)
            {
                yield return completionModule.Execute();
                // 적 격파 조건이면 바로 다음 스텝으로
                if (completionModule is EnemyDefeatedModule edm && edm.IsConditionMet())
                {
                    yield break;
                }
            }

            if (step != null && step.Condition == TutorialCondition.PartPlacement)
            {
                // 배치 완료 즉시 다음 스텝으로 진행
            }
            else if (advanceModule != null)
            {
                yield return advanceModule.Execute();
            }
        }
        finally
        {
            advanceModule?.Cleanup();
            completionModule?.Cleanup();

            for (int i = startupModules.Count - 1; i >= 0; i--)
            {
                startupModules[i].Cleanup();
            }
        }

        EventBus.Instance?.Publish(new TutorialStepCompletedEvent { StepIndex = stepIndex });
    }

    private List<ITutorialModule> BuildStartupModules(TutorialStep step, int stepIndex)
    {
        var modules = new List<ITutorialModule>
        {
            new PauseModule()
        };

        if (step.DialogueConfig != null && step.DialogueConfig.HasDialogue && _dialoguePresenter != null)
        {
            var dialogueModule = new DialogueModule(_dialoguePresenter);
            dialogueModule.SetStepIndex(stepIndex);
            modules.Add(dialogueModule);
        }

        if (step.QuestConfig?.TargetUI != null && _dialoguePresenter != null)
        {
            modules.Add(new QuestModule(_dialoguePresenter));
        }

        if (step.HighlightConfig?.TargetUI != null && _highlighter != null)
        {
            modules.Add(new HighlightModule(_coroutineHost, _highlighter));
        }

        if (step.EnemySpawnConfig?.TutorialStageData != null)
        {
            modules.Add(new EnemySpawnModule());
        }

        return modules;
    }

    private ITutorialModule BuildCompletionModule(TutorialStep step)
    {
        if (_ignoreConditionsForPlaytest || step == null)
        {
            return null;
        }

        if (step.Condition == TutorialCondition.PartPlacement)
        {
            return new PlacementModule();
        }

        if (step.Condition == TutorialCondition.CameraMove)
        {
            return new CameraModule();
        }

        if (step.Condition == TutorialCondition.EnemyDefeated)
        {
            return new EnemyDefeatedModule();
        }

        if (step.Condition == TutorialCondition.InteractionTriggered)
        {
            return new InteractionModule();
        }

        return null;
    }

    private ITutorialModule BuildAdvanceModule(TutorialStep step)
    {
        return new AutoAdvanceModule(
            step.Condition == TutorialCondition.None ? step.AutoAdvanceConfig?.AutoAdvanceDelay ?? 0f : -1f,
            _nextClickedChecker,
            _ignoreConditionsForPlaytest
        );
    }

    private bool ShouldResetCamera(TutorialStep step)
    {
        return step != null
            && step.Condition == TutorialCondition.CameraMove
            && (step.CameraConfig == null || step.CameraConfig.ResetCameraAfterMove);
    }

    private IEnumerator ResetCameraRoutine(float duration)
    {
        CameraManager cameraManager = CameraManager.Instance;
        Camera mainCam = Camera.main;
        if (mainCam == null) yield break;

        Vector3 startPos = mainCam.transform.position;
        float startZoom = mainCam.orthographicSize;
        Vector3 targetPos = cameraManager != null ? cameraManager.OriginalPosition : new Vector3(0f, 0f, CAMERA_DEFAULT_POS_Z);
        float targetZoom = cameraManager != null ? cameraManager.InitialZoom : startZoom;
        float elapsed = 0f;

        if (InputReader.Instance != null) InputReader.Instance.SetInputBlocked(true);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            mainCam.transform.position = Vector3.Lerp(startPos, targetPos, t);
            if (mainCam.orthographic)
            {
                mainCam.orthographicSize = Mathf.Lerp(startZoom, targetZoom, t);
            }
            yield return null;
        }

        mainCam.transform.position = targetPos;
        if (mainCam.orthographic)
        {
            mainCam.orthographicSize = targetZoom;
        }
        if (InputReader.Instance != null) InputReader.Instance.SetInputBlocked(false);
    }
}