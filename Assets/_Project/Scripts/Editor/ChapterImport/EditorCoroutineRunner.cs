#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class EditorCoroutineRunner
{
    public static void Start(IEnumerator routine)
    {
        if (routine == null)
            throw new ArgumentNullException(nameof(routine));

        var state = new EditorCoroutineState(routine);
        EditorApplication.CallbackFunction update = null;
        update = () =>
        {
            try
            {
                if (!state.Tick())
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

    private sealed class EditorCoroutineState
    {
        private readonly Stack<IEnumerator> _stack = new Stack<IEnumerator>();
        private object _waitingOn;

        public EditorCoroutineState(IEnumerator root)
        {
            _stack.Push(root);
        }

        public bool Tick()
        {
            if (IsStillWaiting())
                return true;

            while (_stack.Count > 0)
            {
                IEnumerator current = _stack.Peek();
                if (!current.MoveNext())
                {
                    _stack.Pop();
                    continue;
                }

                if (current.Current is IEnumerator nested)
                {
                    _stack.Push(nested);
                    continue;
                }

                _waitingOn = current.Current;
                return true;
            }

            return false;
        }

        private bool IsStillWaiting()
        {
            if (_waitingOn is AsyncOperation operation && !operation.isDone)
                return true;
            if (_waitingOn is CustomYieldInstruction custom && custom.keepWaiting)
                return true;

            _waitingOn = null;
            return false;
        }
    }
}
#endif
