#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEditor;
using UnityEngine;

public static class EditorCoroutineRunner
{
    public static void Start(IEnumerator routine)
    {
        if (routine == null)
            throw new ArgumentNullException(nameof(routine));

        EditorApplication.CallbackFunction update = null;
        update = () =>
        {
            try
            {
                if (!routine.MoveNext())
                    EditorApplication.update -= update;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.update -= update;
            }
        };

        EditorApplication.update += update;
    }
}
#endif
