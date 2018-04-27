﻿using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[SelectionBase]
public abstract class Character : MonoBehaviour
{
	#region DATA
	// External
	[Header ("Basic settings")]
	public Transform grabHandle;
	public ControllerType control;
	public Color focusColor;
	[Header ("Spell settings")]
	public float spellCooldown;

	// Basic character info
	internal int ID 
	{
		get
		{
			int id = (int) Enum.Parse (typeof(Characters), name);
			return id;
		}
	}

	protected Dictionary<string, Effect> effects;
	internal Locks locks;

	protected SmartAnimator anim;
	protected CharacterController me;
	protected Character other;

	// Locomotion
	internal float speed = 8.5f;
	internal float angularSpeed = 120f;
	internal float gravityMul = 1f;

	// Control
	protected Quaternion targetRotation;
	internal Vector3 movingSpeed;
	internal Vector3 MovingDir 
	{
		get
		{
			return (movingSpeed == Vector3.zero) ?
				transform.forward : movingSpeed.normalized;
		}
	}

	// Capabilities
	internal static float ThrowForce = 20f;

	internal static float DashForce = 40f;
	internal static float DashCooldown = 0.50f;
	protected const float DashDuration = 0.25f;
	protected Coroutine dashCoroutine;

	internal bool knocked;
	internal Coroutine knockedCoroutine;

	protected const float spellSelfStun = 0.75f;
	protected Coroutine spellCoroutine;

	protected Interactable lastMarked;
	internal Grabbable toy;

	// Animation
	public bool Moving 
	{
		get { return anim.GetBool ("Moving"); }
		set { anim.SetBool ("Moving", value); }
	}
	public bool Dashing 
	{
		get { return anim.GetBool ("Dashing"); }
		set { anim.SetBool ("Dashing", value); }
	}
	public bool Carrying 
	{
		get { return anim.GetBool ("Carrying_Stuff"); }
		set { anim.SetBool ("Carrying_Stuff", value); }
	}

#warning I SHOULD TOTALLY KEEP TRACK OF THROW HIT, SKILLS LANDED ETCCCC
	#endregion

	#region LOCOMOTION
	private void Movement () 
	{
		// Get input
		var input = Vector3.zero;
		input.x = Input.GetAxis (GetInputName ("Horizontal"));
		input.z = Input.GetAxis (GetInputName ("Vertical"));

		// Compute rotation equivalent to moving direction
		var dir = Vector3.Min (input, input.normalized);
		if (input != Vector3.zero)
		{
			// Transform direction to be camera-dependent
			dir = TranformToCamera (dir);
			targetRotation = Quaternion.LookRotation (dir);
		}

		// Modify speed to move character
		if (!locks.HasFlag (Locks.Movement))
		{
			movingSpeed = dir * speed;
			// Activate moving animations
			if (input != Vector3.zero) Moving = true;
			else Moving = false;
		}
		else Moving = false;
	}

	private void Rotation () 
	{
		if (locks.HasFlag (Locks.Rotation)) return;

		// Rotate character towards moving directions
		var factor = angularSpeed * Time.deltaTime;
		var newRot = Quaternion.Slerp (transform.rotation, targetRotation, factor);
		transform.rotation = newRot;
	}

	// Actual movement is held here
	private void Move () 
	{
		/// Apply gravity
		var gravity = Physics.gravity * gravityMul;
		var finalSpeed = movingSpeed + gravity;

		/// Move player
		me.Move (finalSpeed * Time.deltaTime);
#warning test if collision flags are useful or what
	}
	#endregion

	#region TOY
	// Keeps toy with the character
	private void HoldToy () 
	{
		if (toy == null) return;

		// Make toy follow smoothly
		var newPos = Vector3.Lerp (toy.body.position, grabHandle.position, Time.fixedDeltaTime * 7f);
		toy.body.MovePosition (newPos);
	}
	#endregion

	#region DASHING
	private void Dash () 
	{
		if (locks.HasFlag (Locks.Dash)) return;		// Is Dash up?
		else if (!GetButtonDown ("Dash")) return;   // Has user pressed the button?
		else if (toy) return;                       // Can't dash while holding stuff
		// If everything's ok
		else Dashing = true;

		// Start dash & put in cooldoown
		AddCC ("-> Dash", Locks.Dash);				// Self CC used as cooldown
		AddCC ("Dashing", Locks.Locomotion);
		dashCoroutine = StartCoroutine (InDash ());
#warning test if coroutines exist after they end
	}

	private IEnumerator InDash () 
	{
		// Cache gravity & reduce its
		float oldGravity = gravityMul;
		gravityMul = 0.6f;

		float factor = 0f;
		bool knockOcurred = false;
		while (factor <= 1.1f) 
		{
			// Move player at dash speed (slow as closer to end)
			movingSpeed = MovingDir * DashForce * (1f - factor);

			// Knock other character back if hit
			var dist = Vector3.Distance (transform.position, other.transform.position);
			if (dist <= 0.8 && !knockOcurred)
			{
				// Get force from movement & supress Y-force
				var force = MovingDir;
				force.y = 0f;

				other.Knock (force, 0.25f);

				// Hard-slow dash & avoid knocking again
				knockOcurred = true;
				factor = 0.7f;

			}

			factor += Time.deltaTime / DashDuration;
			yield return null;
		}
		// Reset
		RemoveCC ("Dashing");
		RemoveCC ("-> Dash");
		Dashing = false;

		// Restore gravity
		gravityMul = oldGravity;
	}
	#endregion

	#region KNOCKING
	public void Knock (Vector3 dir, float duration) 
	{
		// Only add CC if wasn't already knocked
		if (!effects.ContainsKey ("Knocked")) AddCC ("Knocked", Locks.All);
		knockedCoroutine = StartCoroutine (KnockingTo (dir, duration));

		// Let go grabbed object, if any,
		// in opposite direction of knock
		if (toy) toy.Throw (-dir * 5f, owner: this);
	}

	private IEnumerator KnockingTo (Vector3 dir, float duration) 
	{
		var factor = 0f;
		while (factor <= 1f)
		{
			/// Move player during knock
			movingSpeed = dir * DashForce * (1f - factor);

			/// Rotate player 'cause its cool
			transform.Rotate (Vector3.up, 771f * Time.deltaTime);
			targetRotation = transform.rotation;

			factor += Time.deltaTime / duration;
			yield return null;
		}
		yield return new WaitForSeconds (0.1f);
		effects.Remove ("Knocked");
		knocked = false;
	}
	#endregion

	#region SPELL CASTING
	private void CheckSpell ()
	{
		if (locks.HasFlag (Locks.Spells)) return;
		if (!GetButtonDown ("Special")) return;
		if (toy) return;

		// If everything's ok
		var block = (Locks.Locomotion | Locks.Interaction);
		AddCC ("Spell Casting", block, spellSelfStun);

		// Self CC used as cooldown
		AddCC ("-> Spell", Locks.Spells);

		// Cast spell & put it on CD
		anim.SetTrigger ("Cast_Spell");
		StartCoroutine (WaitSpellCD ());
		spellCoroutine = StartCoroutine (CastSpell ());
	}
	protected abstract IEnumerator CastSpell ();

	// Just wait until CD is over
	private IEnumerator WaitSpellCD () 
	{
		yield return new WaitForSeconds (spellCooldown);
		RemoveCC ("-> Spell");
	}
	#endregion

	#region INTERACTION
	private void CheckInteractions () 
	{
		if (locks.HasFlag (Locks.Interaction)) return;

		var ray = NewRay ();
		var hit = new RaycastHit ();
		if (Physics.Raycast (ray, out hit, 2f, 1 << 8 | 1 << 10))
		{
			#warning this doesnt work with grab helpers?
			var interactable = hit.collider.GetComponent<Interactable> ();

			// If valid target
			if (interactable && interactable.CheckInteraction (this))
			{
				if (!lastMarked)
				{
					// Focus object
					interactable.marker.On (focusColor);
					lastMarked = interactable;

					// Register player if it's a machine
					var m = interactable as MachineInterface;
					if (m) m.PlayerIsNear (near: true);
				}
				if (GetButtonDown ("Action")) interactable.Action (this);
				return;
			}
		}

		// If not in front of any interactable
		// de-mark last one seen, if any
		if (lastMarked)
		{
			// Un-register player if it's a machine
			var m = lastMarked as MachineInterface;
			if (m) m.PlayerIsNear (near: false);

			// Un-focus
			lastMarked.marker.Off ();
			lastMarked = null;
		}

		// If not in front of any interactable,
		// or not executed any interaction
		if (GetButtonDown ("Action", true) && toy)
			toy.Throw (MovingDir * ThrowForce, owner: this);
	}

	#endregion

	#region EFFECTS MANAGEMENT
	private void ReadEffects () 
	{
		locks = Locks.NONE;
		foreach (var e in effects)
		{
			// Resets CCs & then reads them every frame
			locks = locks.SetFlag (e.Value.cc);
		}
	}

	// Helper for only adding CCs
	public void AddCC (string name, Locks cc, float duration = 0)
	{
		var e = new Effect () { cc = cc };

		effects.Add (name, e);
		if (duration != 0) StartCoroutine (RemoveEffectAfter (name, duration));

		// Interrupt capabilities
		if (cc.HasFlag (Locks.Movement))
		{
			movingSpeed = Vector3.zero;
			if (dashCoroutine != null) StopCoroutine (dashCoroutine);
			if (knockedCoroutine != null) StopCoroutine (knockedCoroutine);
		}
		else
		if (cc.HasFlag (Locks.Spells) && spellCoroutine != null)
			StopCoroutine (spellCoroutine);
	}

	// Helper for manually removing a CC effect
	public void RemoveCC (string name) 
	{
		if (effects.ContainsKey (name))
			effects.Remove (name);
	}

	// Internal helper for temporal CCs
	IEnumerator RemoveEffectAfter (string name, float duration) 
	{
		yield return new WaitForSeconds (duration);
		effects.Remove (name);
	}
	#endregion

	#region HELPERS
	// Get coorrect camera-dependent vector
	private Vector3 TranformToCamera (Vector3 dir) 
	{
		var cam = Camera.main.transform;
		// Ignore rotation (except Y) 
		var rot = cam.eulerAngles;
		rot.x = 0;
		rot.z = 0;
		#warning really bad practice man
		return Matrix4x4.TRS (cam.position, Quaternion.Euler (rot), Vector3.one).MultiplyVector (dir);
	}

	// Returns 'Ray' for checking interactions
	private Ray NewRay () 
	{
		// Generates ray for raycasting
		var origin = transform.position;
		origin.y += 0.75f + 0.15f;
		return new Ray (origin, transform.forward);
	}

	// Gets instance of specific character
	public static Character Get<T> () where T : Character
	{
		var c = GameObject.Find (typeof (T).Name);
		return c.GetComponent<T> ();
	}

	#region SPECIAL INPUT HELPERS
	/* Gets input based on player controller and
	taking into account if it's already been consumed*/

	List<string> consumedInputs;
	public bool GetButtonDown (string button, bool consume = true)
	{
		var input = GetInputName (button);
		// If not consumed, return input value
		if (!consumedInputs.Contains (input)) 
		{
			var pressed = Input.GetButtonDown (input);
			if (pressed && consume) consumedInputs.Add (input);
			return pressed;
		}
		else return false;
	}

	// Generates correct name
	public string GetInputName (string input)
	{
		var prefix = control.ToString ();
		return prefix + "_" + input;
	}

	private void ResetInputs () { consumedInputs.Clear (); }
	#endregion
	#endregion

	#region UNITY CALLBACKS
	protected virtual void Update ()
	{
		// Initialization
		ResetInputs ();
		ReadEffects ();

		if (Game.paused)
		{
			// Stop player
			Moving = false;
			// Skip all other actions
			return;
		}

		/// Locomotion
		Movement ();
		Rotation ();
		Dash ();
		Move ();

		// Interaction
		CheckInteractions ();
		CheckSpell ();
	}

	protected virtual void Awake () 
	{
		// Find other player
		other = FindObjectsOfType<Character> ().First (c => c != this);

		// Initialize stuff
		anim = new SmartAnimator (GetComponent<Animator> ());
		effects = new Dictionary<string, Effect> ();
		consumedInputs = new List<string> ();

		// Get some references
		me = GetComponent<CharacterController> ();
		targetRotation = transform.rotation;
	}

	protected virtual void FixedUpdate () 
	{
		HoldToy ();
	}
	#endregion
}
