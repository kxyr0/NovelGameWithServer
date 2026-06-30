using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

[AddComponentMenu("Novel Template/UI/Game Object Toggle")]
[DisallowMultipleComponent]
public class GameObjectToggle : Selectable, IPointerClickHandler, ISubmitHandler
{
	[Serializable]
	public sealed class ToggleEvent : UnityEvent<bool>
	{
	}

	[Header("State")]
	[SerializeField]
	[FormerlySerializedAs("isOn")]
	private bool _isOn;

	[SerializeField]
	[FormerlySerializedAs("toggleOnClick")]
	private bool _toggleOnClick = true;

	[SerializeField]
	[FormerlySerializedAs("allowSwitchOff")]
	private bool _allowSwitchOff = true;

	[SerializeField]
	[FormerlySerializedAs("applyOnEnable")]
	private bool _applyOnEnable = true;

	[Header("Targets")]
	[SerializeField]
	[FormerlySerializedAs("onObjects")]
	private GameObject[] _onObjects = Array.Empty<GameObject>();

	[SerializeField]
	[FormerlySerializedAs("offObjects")]
	private GameObject[] _offObjects = Array.Empty<GameObject>();

	[Header("Events")]
	[SerializeField]
	[FormerlySerializedAs("onValueChanged")]
	private ToggleEvent _onValueChanged = new ToggleEvent();

	private bool _hasAwoken;

	public event Action<bool> ValueChanged;

	public bool IsOn
	{
		get => _isOn;
		set => SetIsOn(value);
	}

	// Keep Unity Toggle's familiar API name for drop-in style call sites.
	public bool isOn
	{
		get => _isOn;
		set => SetIsOn(value);
	}

	public bool ToggleOnClick
	{
		get => _toggleOnClick;
		set => _toggleOnClick = value;
	}

	public bool AllowSwitchOff
	{
		get => _allowSwitchOff;
		set => _allowSwitchOff = value;
	}

	public bool ApplyOnEnable
	{
		get => _applyOnEnable;
		set => _applyOnEnable = value;
	}

	public int OnObjectCount => _onObjects != null ? _onObjects.Length : 0;
	public int OffObjectCount => _offObjects != null ? _offObjects.Length : 0;
	public ToggleEvent OnValueChanged => _onValueChanged;

	protected override void Awake()
	{
		base.Awake();
		NormalizeSerializedState();
		_hasAwoken = true;
		ApplyState();
	}

	protected override void OnEnable()
	{
		base.OnEnable();

		if (_applyOnEnable || !_hasAwoken)
			ApplyState();
	}

	protected override void OnValidate()
	{
		base.OnValidate();
		NormalizeSerializedState();

		if (!Application.isPlaying)
			ApplyState();
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		if (eventData != null && eventData.button != PointerEventData.InputButton.Left)
			return;

		ToggleFromInput();
	}

	public void OnSubmit(BaseEventData eventData)
	{
		ToggleFromInput();
	}

	public void Toggle()
	{
		SetIsOn(!_isOn);
	}

	public void TurnOn()
	{
		SetIsOn(true);
	}

	public void TurnOff()
	{
		SetIsOn(false);
	}

	public void SetIsOn(bool value)
	{
		SetIsOn(value, true);
	}

	public void SetIsOn(bool value, bool sendCallback)
	{
		SetIsOnInternal(value, sendCallback);
	}

	public void SetIsOnWithoutNotify(bool value)
	{
		SetIsOnInternal(value, false);
	}

	public void ApplyState()
	{
		NormalizeSerializedState();
		SetTargetsActive(_onObjects, _isOn);
		SetTargetsActive(_offObjects, !_isOn, _isOn ? _onObjects : null);
	}

	public void SetTargets(GameObject onObject, GameObject offObject)
	{
		_onObjects = CreateSingleTargetArray(onObject);
		_offObjects = CreateSingleTargetArray(offObject);
		ApplyState();
	}

	public void SetTargets(IEnumerable<GameObject> onObjects, IEnumerable<GameObject> offObjects)
	{
		_onObjects = CopyTargets(onObjects);
		_offObjects = CopyTargets(offObjects);
		ApplyState();
	}

	public void SetOnObjects(IEnumerable<GameObject> onObjects)
	{
		_onObjects = CopyTargets(onObjects);
		ApplyState();
	}

	public void SetOffObjects(IEnumerable<GameObject> offObjects)
	{
		_offObjects = CopyTargets(offObjects);
		ApplyState();
	}

	public void AddOnObject(GameObject target)
	{
		_onObjects = AddTarget(_onObjects, target);
		ApplyState();
	}

	public void AddOffObject(GameObject target)
	{
		_offObjects = AddTarget(_offObjects, target);
		ApplyState();
	}

	public void RemoveOnObject(GameObject target)
	{
		_onObjects = RemoveTarget(_onObjects, target);
		ApplyState();
	}

	public void RemoveOffObject(GameObject target)
	{
		_offObjects = RemoveTarget(_offObjects, target);
		ApplyState();
	}

	public void ClearTargets()
	{
		_onObjects = Array.Empty<GameObject>();
		_offObjects = Array.Empty<GameObject>();
	}

	public GameObject GetOnObject(int index)
	{
		return GetTarget(_onObjects, index);
	}

	public GameObject GetOffObject(int index)
	{
		return GetTarget(_offObjects, index);
	}

	public void AddValueChangedListener(UnityAction<bool> listener)
	{
		if (listener != null)
			_onValueChanged.AddListener(listener);
	}

	public void RemoveValueChangedListener(UnityAction<bool> listener)
	{
		if (listener != null)
			_onValueChanged.RemoveListener(listener);
	}

	public void RemoveAllValueChangedListeners()
	{
		_onValueChanged.RemoveAllListeners();
		ValueChanged = null;
	}

	private bool SetIsOnInternal(bool value, bool sendCallback)
	{
		if (!_allowSwitchOff && !value && _isOn)
			return false;

		if (_isOn == value)
		{
			ApplyState();
			return false;
		}

		_isOn = value;
		ApplyState();

		if (sendCallback)
			NotifyValueChanged();

		return true;
	}

	private void ToggleFromInput()
	{
		if (!_toggleOnClick || !IsActive() || !IsInteractable())
			return;

		Toggle();
	}

	private void NotifyValueChanged()
	{
		_onValueChanged.Invoke(_isOn);
		ValueChanged?.Invoke(_isOn);
	}

	private void NormalizeSerializedState()
	{
		if (_onObjects == null)
			_onObjects = Array.Empty<GameObject>();

		if (_offObjects == null)
			_offObjects = Array.Empty<GameObject>();

		if (_onValueChanged == null)
			_onValueChanged = new ToggleEvent();
	}

	private void SetTargetsActive(GameObject[] targets, bool active, GameObject[] activeDescendants = null)
	{
		if (targets == null)
			return;

		for (int i = 0; i < targets.Length; i++)
		{
			GameObject target = targets[i];
			if (!active && HasActiveDescendantTarget(target, activeDescendants))
				continue;

			if (target != null && target.activeSelf != active)
				target.SetActive(active);
		}
	}

	private bool HasActiveDescendantTarget(GameObject target, GameObject[] activeDescendants)
	{
		if (target == null || activeDescendants == null)
			return false;

		Transform targetTransform = target.transform;
		for (int i = 0; i < activeDescendants.Length; i++)
		{
			GameObject activeDescendant = activeDescendants[i];
			if (activeDescendant == null)
				continue;

			Transform activeTransform = activeDescendant.transform;
			if (activeTransform == targetTransform || activeTransform.IsChildOf(targetTransform))
				return true;
		}

		return false;
	}

	private GameObject[] CopyTargets(IEnumerable<GameObject> targets)
	{
		if (targets == null)
			return Array.Empty<GameObject>();

		List<GameObject> copiedTargets = new List<GameObject>();
		foreach (GameObject target in targets)
		{
			if (target != null && !copiedTargets.Contains(target))
				copiedTargets.Add(target);
		}

		return copiedTargets.Count > 0 ? copiedTargets.ToArray() : Array.Empty<GameObject>();
	}

	private GameObject[] CreateSingleTargetArray(GameObject target)
	{
		return target != null
			? new[] { target }
			: Array.Empty<GameObject>();
	}

	private GameObject[] AddTarget(GameObject[] targets, GameObject target)
	{
		if (target == null)
			return targets ?? Array.Empty<GameObject>();

		targets ??= Array.Empty<GameObject>();
		if (ContainsTarget(targets, target))
			return targets;

		GameObject[] result = new GameObject[targets.Length + 1];
		Array.Copy(targets, result, targets.Length);
		result[result.Length - 1] = target;
		return result;
	}

	private GameObject[] RemoveTarget(GameObject[] targets, GameObject target)
	{
		if (targets == null || targets.Length == 0 || target == null)
			return targets ?? Array.Empty<GameObject>();

		List<GameObject> result = new List<GameObject>();
		for (int i = 0; i < targets.Length; i++)
		{
			GameObject current = targets[i];
			if (current != null && current != target)
				result.Add(current);
		}

		return result.Count > 0 ? result.ToArray() : Array.Empty<GameObject>();
	}

	private bool ContainsTarget(GameObject[] targets, GameObject target)
	{
		if (targets == null || target == null)
			return false;

		for (int i = 0; i < targets.Length; i++)
		{
			if (targets[i] == target)
				return true;
		}

		return false;
	}

	private GameObject GetTarget(GameObject[] targets, int index)
	{
		if (targets == null || index < 0 || index >= targets.Length)
			return null;

		return targets[index];
	}
}
