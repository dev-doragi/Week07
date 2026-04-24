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
