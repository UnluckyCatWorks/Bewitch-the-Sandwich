﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HUDIcons : MonoBehaviour
{
	private Dictionary<string, SpriteRenderer> icons;
	Transform current;

	public void Show (string name) 
	{
		var s = icons[name];
		StartCoroutine (Fade(s, 1f));
		current = s.transform;
	}
	public void Hide (string name) 
	{
		var s = icons[name];
		StartCoroutine (Fade (s, 0f));
	}

	IEnumerator Fade (SpriteRenderer s, float target) 
	{
		float iAlpha = 0f;
		float factor = 0f;
		float duration = 0.2f;
		while (factor <= 1.1f)
		{
			float x = Mathf.Lerp (iAlpha, target, factor);
			s.SetAlpha (x);

			yield return null;
			factor += Time.deltaTime / duration;
		}
		if (target <= 0f) current = null;
	}

	private void Update () 
	{
		if (!current) return;
		var dir = Camera.main.transform.position - current.position;
		current.rotation = Quaternion.LookRotation (-dir.normalized);
	}

	private void Awake () 
	{
		icons = new Dictionary<string, SpriteRenderer> ();
		for (int i=0; i!=transform.childCount; i++)
		{
			var c = transform.GetChild (i);
			var s = c.GetComponent<SpriteRenderer> ();
			icons.Add (c.name, s);
			s.SetAlpha (0);
		}
	}
}
