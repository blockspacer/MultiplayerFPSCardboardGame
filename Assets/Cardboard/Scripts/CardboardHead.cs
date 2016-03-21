﻿// Copyright 2014 Google Inc. All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;
using UnityEngine.UI;

/// @ingroup Scripts
/// This script provides head tracking support for a camera.
///headAxis
/// Attach this script to any game object that should match the user's head motion.
/// By default, it continuously updates the local transform to Cardboard.HeadView.
/// A target object may be specified to provide an alternate reference frame for the motion.
///
/// This script will typically be attached directly to a Camera object, or to its
/// parent if you need to offset the camera from the origin.
/// Alternatively it can be inserted as a child of the Camera and parent of the
/// CardboardEye camera.  Do this if you already have steering logic driving the
/// mono Camera and wish to have the user's head motion be relative to that.  Note
/// that in the latter setup, head tracking is visible only when VR Mode is enabled.
///
/// In some cases you may need two instances of CardboardHead, referring to two
/// different targets (one of which may be the parent), in order to split where
/// the rotation is applied from where the positional offset is applied.  Use the
/// #trackRotation and #trackPosition properties in this case.
public class CardboardHead : MonoBehaviour
{
	/// Determines whether to apply the user's head rotation to this gameobject's
	/// orientation.  True means to update the gameobject's orientation with the
	/// user's head rotation, and false means don't modify the gameobject's orientation.
	public bool trackRotation = true;

	/// Determines whether to apply ther user's head offset to this gameobject's
	/// position.  True means to update the gameobject's position with the user's head offset,
	/// and false means don't modify the gameobject's position.
	public bool trackPosition = true;

	/// The user's head motion will be applied in this object's reference frame
	/// instead of the head object's parent.  A good use case is for head-based
	/// steering.  Normally, turning the parent object (i.e. the body or vehicle)
	/// towards the direction the user is looking would carry the head along with it,
	/// thus creating a positive feedback loop.  Use an external target object as a
	/// fixed point of reference for the direction the user is looking.  Often, the
	/// grandparent or higher ancestor is a suitable target.
	public Transform target;

	//assing player head here to rotate the head as cardboard is tilt
	[HideInInspector]
	private Transform characterHead;

	//assing player body here to get to the rotation of the body relative to the head
	[HideInInspector]
	public Transform player;

	private bool syncHead = false;

	/// Determines whether the head tracking is applied during `LateUpdate()` or
	/// `Update()`.  The default is false, which means it is applied during `LateUpdate()`
	/// to reduce latency.
	///
	/// However, some scripts may need to use the camera's direction to affect the gameplay,
	/// e.g by casting rays or steering a vehicle, during the `LateUpdate()` phase.
	/// This can cause an annoying jitter because Unity, during this `LateUpdate()`
	/// phase, will update the head object first on some frames but second on others.
	/// If this is the case for your game, try switching the head to apply head tracking
	/// during `Update()` by setting this to true.
	public bool updateEarly = false;

	//-------------------------------------------------------------------------------
	[HideInInspector]
	public bool isAimHit;
	[HideInInspector]
	public RaycastHit shootHit;
	[HideInInspector]
	public Vector3 aimPoint;
	[HideInInspector]
	public int aimMask;
	public string raycastingMask;
	[HideInInspector]
	public bool isCharacterSync = false;
	//-------------------------------------------------------------------------------

	void Start() {
		ConsoleLog.SLog ("CardboardHead Start()");

		try {
			characterHead = GameObject.FindGameObjectWithTag ("CharacterHead").transform;
			player = GameObject.FindGameObjectWithTag ("Player").transform;
			aimMask = LayerMask.GetMask (raycastingMask);
			isCharacterSync = true;
		} catch (System.Exception e){
			ConsoleLog.SLog ("Error in CardboardHead Start()\n" + e.Message);
		}

		if (characterHead != null && player != null) {
			syncHead = true;
		}
	}

	public void ReSyncCharacter(){
		ConsoleLog.SLog ("CardboardHead ReSyncCharacter()");

		try {
			characterHead = GameObject.FindGameObjectWithTag ("CharacterHead").transform;
			player = GameObject.FindGameObjectWithTag ("Player").transform;
			isCharacterSync = true;
		} catch (System.Exception e){
			ConsoleLog.SLog ("Error in CardboardHead ReSyncCharacter()\n" + e.Message);
		}
	}

	public Ray Gaze {
		get {
			UpdateHead ();
			return new Ray (transform.position, transform.forward);
		}
	}

	private bool updated;

	void Update ()
	{
		updated = false;  // OK to recompute head pose.
		if (updateEarly) {
			UpdateHead ();
		}

	}

	// Normally, update head pose now.
	void LateUpdate ()
	{
		UpdateHead ();

		//update aiming position
		getAimPoint(out shootHit, out aimPoint);
	}

	// Compute new head pose.
	private void UpdateHead ()
	{
		if (updated) {  // Only one update per frame, please.
			return;
		}
		updated = true;
		Cardboard.SDK.UpdateState ();

		if (trackRotation) {
			var rot = Cardboard.SDK.HeadPose.Orientation;
			if (target == null) {
				//rotate cardboard camera
				transform.localRotation = rot;

				if (!syncHead) {
					return;
				}

				if (characterHead == null || player == null) {
					ReSyncCharacter ();
				}

				//convert cardboard quaternion to angle-axis
				float cardboardAngle = 0.0f;
				Vector3 cardboardAxis = Vector3.zero;
				rot.ToAngleAxis (out cardboardAngle, out cardboardAxis);

				//lock y axis rotation for head, swap some axis to correct orientation
				Vector3 cardboardAxisLockY = rot.eulerAngles;
				float temp = cardboardAxisLockY.x;
				cardboardAxisLockY.y = cardboardAxisLockY.z;
				cardboardAxisLockY.z = -temp;
				cardboardAxisLockY.x = 0;

				//rotate head
				characterHead.localRotation = Quaternion.Euler(cardboardAxisLockY);

				//lock x and z rotation for body
				Vector3 cardboardAxisLockXZ = rot.eulerAngles;
				cardboardAxisLockXZ.x = cardboardAxisLockXZ.z = 0;

				//rotate body
				player.rotation = Quaternion.Euler(cardboardAxisLockXZ);

			} else {
				transform.rotation = target.rotation * rot;
			}
		}

		if (trackPosition) {
			Vector3 pos = Cardboard.SDK.HeadPose.Position;
			if (target == null) {
				transform.localPosition = pos;
			} else {
				transform.position = target.position + target.rotation * pos;
			}
		}
	}

	public bool getAimPoint(out RaycastHit hit, out Vector3 aimPoint, int range = 100){
		
		if (Physics.Raycast (Gaze, out hit, range, aimMask)) {
			isAimHit = true;
			aimPoint = hit.point;
		} else {
			isAimHit = false;
			aimPoint = transform.position + transform.forward * range;
		}

		return isAimHit;
	}
}
