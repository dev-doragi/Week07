using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class BackgroundManager : MonoBehaviour
{
    [SerializeField] private List<ParallaxBackground> backgrounds = new List<ParallaxBackground>();

    private void Awake()
    {
        RegisterSceneBackgrounds();
    }

    private void OnEnable()
    {
        EventBus.Instance?.Subscribe<WaveStartedEvent>(OnWaveStarted);
        EventBus.Instance?.Subscribe<InGameStateChangedEvent>(OnInGameStateChanged);
    }

    private void OnDisable()
    {
        EventBus.Instance?.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
        EventBus.Instance?.Unsubscribe<InGameStateChangedEvent>(OnInGameStateChanged);
    }

    private void OnWaveStarted(WaveStartedEvent evt)
    {
        StopAllBackgrounds();
    }

    private void OnInGameStateChanged(InGameStateChangedEvent evt)
    {
        if (evt.NewState == InGameState.Prepare)
            RestartAllBackgrounds();
    }
    

    public void RegisterSceneBackgrounds()
    {
        backgrounds.Clear();

        ParallaxBackground[] foundBackgrounds =
        Object.FindObjectsByType<ParallaxBackground>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        for (int i = 0; i < foundBackgrounds.Length; i++)
        {
            ParallaxBackground background = foundBackgrounds[i];
            if (background != null)
            {
                backgrounds.Add(background);
            }
        }
    }

    public void StopAllBackgrounds()
    {
        for (int i = 0; i < backgrounds.Count; i++)
        {
            ParallaxBackground background = backgrounds[i];
            if (background != null)
            {
                background.StopBackground();
            }
        }
    }

    public void RestartAllBackgrounds()
    {
        for (int i = 0; i < backgrounds.Count; i++)
        {
            ParallaxBackground background = backgrounds[i];
            if (background != null)
            {
                background.RestartBackground();
            }
        }
    }
}
