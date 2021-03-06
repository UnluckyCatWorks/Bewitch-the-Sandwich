﻿using UnityEngine;
using UnityEngine.Animations;
using System;
using System.Collections;
using System.Collections.Generic;

public class MachineController : StateMachineBehaviour 
{
	#region DATA
	internal MachineState state;            // The current status of the machine
	internal SmartAnimator anim;            // The machine animator
	protected MachineInterface bridge;      // The link with the actual machine parameters

	private float clock; 
	#endregion

	#region CALLBACKS
	public override void OnStateEnter (Animator animator, AnimatorStateInfo stateInfo, int layerIndex) 
	{
		base.OnStateEnter(animator, stateInfo, layerIndex);
		if (stateInfo.IsName("Base.Waiting"))
		{
			// Initizalize references
			if (bridge == null) 
			{
				anim = new SmartAnimator (animator);
				bridge = animator.GetComponent<MachineInterface>();
			}
			OnEnterWaiting();
		}
		if (stateInfo.IsName("Base.Working"))		OnEnterWorking ();
		if (stateInfo.IsName("Base.Completed"))		OnEnterCompleted ();
		if (stateInfo.IsName("Base.Overheating"))	OnEnterOverheat ();
		if (stateInfo.IsName("Base.Overload"))		OnEnterOverload ();

	}

	public override void OnStateUpdate (Animator animator, AnimatorStateInfo stateInfo, int layerIndex) 
	{
		base.OnStateUpdate(animator, stateInfo, layerIndex);
		if (stateInfo.IsName("Base.Waiting"))		OnUpdateWaiting ();
		if (stateInfo.IsName("Base.Working"))		OnUpdateWorking ();
		if (stateInfo.IsName("Base.Completed"))		OnUpdateCompleted ();
		if (stateInfo.IsName("Base.Overheating"))	OnUpdateOverheat ();
		if (stateInfo.IsName("Base.Overload"))		OnUpdateOverload ();

	}

	public override void OnStateExit (Animator animator, AnimatorStateInfo stateInfo, int layerIndex) 
	{
		base.OnStateExit(animator, stateInfo, layerIndex);
		if (stateInfo.IsName("Base.Waiting"))		OnExitUnready ();
		if (stateInfo.IsName("Base.Working"))		OnExitWorking ();
		if (stateInfo.IsName("Base.Completed"))		OnExitCompleted ();
		if (stateInfo.IsName("Base.Overheating"))	OnExitOverheat ();
		if (stateInfo.IsName("Base.Overload"))		OnExitOverload ();
	}
	#endregion

	#region WAITING
	public virtual void OnEnterWaiting () 
	{
		state = MachineState.Waiting;
	}
	public virtual void OnUpdateWaiting () { }
	public virtual void OnExitUnready () { } 
	#endregion

	#region WORKING
	public virtual void OnEnterWorking ()
	{
		state = MachineState.Working;
		clock = 0f;
	}
	public virtual void OnUpdateWorking () 
	{
		if (anim.Animator.IsInTransition(0)) return;
		if (clock > bridge.duration)
		{
			// Complete the work if time has finished
			anim.SetTrigger("Work_Completed");
		}
		else
		if (!Game.stopped) 
		{
			var factor = clock / bridge.duration;
			clock += Time.deltaTime;
		}
	}
	public virtual void OnExitWorking () { } 
	#endregion

	#region COMPLETION
	public virtual void OnEnterCompleted ()
	{
		state = MachineState.Completed;
		bridge.ProcessObject();
		clock = 0f;
	}
	public virtual void OnUpdateCompleted () 
	{
		if (anim.Animator.IsInTransition(0)) return;
		if (clock > bridge.safeTime) 
		{
			/// If time runs out and player hasn't
			/// picked up yet, start overheating
			anim.SetTrigger("Start_Overheat");
		}
		else
		if (!Game.stopped) clock += Time.deltaTime;
	}
	public virtual void OnExitCompleted () { }
	#endregion

	#region OVERHEAT
	public virtual void OnEnterOverheat ()
	{
		state = MachineState.Overheating;
		clock = 0f;
	}
	public virtual void OnUpdateOverheat ()
	{
		if (anim.Animator.IsInTransition(0)) return;
		if (clock > bridge.overheatTime)
		{
			// If time runs out and player hasn't
			// picked up yet, go full overload
			anim.SetTrigger("Start_Overload");

			/// De-parent ingredient
			bridge.obj.transform.SetParent (null);
		}
		else
		if (!Game.stopped)
		{
			var factor = clock / bridge.overheatTime;
			clock += Time.deltaTime;
		}
	}
	public virtual void OnExitOverheat () { } 
	#endregion

	#region OVERLOAD
	public virtual void OnEnterOverload () 
	{
		state = MachineState.Overloading;
	}
	public virtual void OnUpdateOverload () { }
	public virtual void OnExitOverload () { }  
	#endregion
}
