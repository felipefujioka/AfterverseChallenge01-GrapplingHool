using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class RemoveArmatureFromClip: EditorWindow
{
    private AnimationClip _clip;
    private AnimationClip _targetClip;

    [MenuItem("Custom Tools/Remove armature")]
    public static void OpenWindow()
    {
        RemoveArmatureFromClip window = (RemoveArmatureFromClip)EditorWindow.GetWindow(typeof(RemoveArmatureFromClip));
        window.Show();
    }

    private void OnGUI()
    {
        _clip = (AnimationClip) EditorGUILayout.ObjectField(_clip, typeof(AnimationClip), false);
        _targetClip = (AnimationClip) EditorGUILayout.ObjectField(_targetClip, typeof(AnimationClip), false);

        if (_clip != null)
        {
            if (GUILayout.Button("Clean clip"))
            {
                CleanClip();
            }
        }
    }

    private void CleanClip()
    {
        var bindings = AnimationUtility.GetCurveBindings(_clip);
        var newBindings = new List<EditorCurveBinding>();    
        var curves = new List<AnimationCurve>();
        foreach (var binding in bindings)
        {
            var curve = AnimationUtility.GetEditorCurve(_clip, binding);
            curves.Add(curve);

            var newBinding = new EditorCurveBinding
            {
                path = binding.path.Replace("Armature/", ""), 
                propertyName = binding.propertyName,
                type = binding.type
            };

            newBindings.Add(newBinding);
        }

        if (newBindings.Count == curves.Count)
        {
            AnimationUtility.SetEditorCurves(_targetClip, newBindings.ToArray(), curves.ToArray());
        }
    }
}