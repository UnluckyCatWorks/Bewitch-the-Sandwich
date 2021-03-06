﻿using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[CreateAssetMenu (fileName = "New Dialog", menuName = "New dialog", order = 1000)]
public class Speech : ScriptableObject
{
	public Dialog[] dialog;

	[Serializable]
	public struct Dialog
	{
		[TextArea]
		public string message;
		public float speed;
		public string trigger;
	}
}
