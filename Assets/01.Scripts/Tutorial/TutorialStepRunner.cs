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
                if ((completionModule is EnemyDefeatedModule edm && edm.IsConditionMet()) ||
                    (completionModule is InteractionModule im && im.IsConditionMet()))
                {
                    yield break;
                }
            }

            bool keepCamera = false;
            foreach (var module in startupModules)
            {
                if (module is CameraModule camModule && camModule.KeepCameraSize)
                {
                    keepCamera = true;
                    break;
                }
            }
            if (!keepCamera)
            {
                CameraManager cameraManager = CameraManager.Instance;
                Camera mainCam = Camera.main;
                if (cameraManager != null && mainCam != null)
                {
                    float current = mainCam.orthographicSize;
                    float initial = cameraManager.InitialZoom;
                    if (!Mathf.Approximately(current, initial))
                        yield return cameraManager.SmoothResetZoom(0.3f);
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

    private IEnumerator ResetCameraRoutine(float duration)
    {
        CameraManager cameraManager = CameraManager.Instance;
        Camera mainCam = Camera.main;
        if (mainCam == null || cameraManager == null) yield break;

        // 작업을 CameraManager.SmoothResetZoom으로 위임
        yield return cameraManager.SmoothResetZoom(duration);
    }
}