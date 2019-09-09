using AvatarScriptPack;
using System;
using UnityEngine;

namespace CustomAvatar
{
	public class AvatarBehaviour : MonoBehaviour
	{
		public static PosRot? LeftLegCorrection { get; set; }
		public static PosRot? RightLegCorrection { get; set; }
		public static PosRot? PelvisCorrection { get; set; }

		private Transform _head;
		private Transform _body;
		private Transform _leftHand;
		private Transform _rightHand;
		private Transform _leftLeg;
		private Transform _rightLeg;
		private Transform _pelvis;

		private Vector3 _prevBodyPos;

		private Vector3 _prevLeftLegPos = default(Vector3);
		private Vector3 _prevRightLegPos = default(Vector3);
		private Quaternion _prevLeftLegRot = default(Quaternion);
		private Quaternion _prevRightLegRot = default(Quaternion);

		private Vector3 _prevPelvisPos = default(Vector3);
		private Quaternion _prevPelvisRot = default(Quaternion);

		private VRIK _vrik;
		private IKManagerAdvanced _ikManagerAdvanced;
		private TrackedDeviceManager _trackedDevices;
		private VRPlatformHelper _vrPlatformHelper;

		public void Start()
		{
			_vrik = GetComponentInChildren<VRIK>();
			_ikManagerAdvanced = GetComponentInChildren<IKManagerAdvanced>();
			_trackedDevices = PersistentSingleton<TrackedDeviceManager>.instance;
			_vrPlatformHelper = PersistentSingleton<VRPlatformHelper>.instance;

			_trackedDevices.DeviceAdded += (device) => UpdateVrikReferences();
			_trackedDevices.DeviceRemoved += (device) => UpdateVrikReferences();

			_head = GetHeadTransform();
			_body = gameObject.transform.Find("Body");
			_leftHand = gameObject.transform.Find("LeftHand");
			_rightHand = gameObject.transform.Find("RightHand");
			_leftLeg = gameObject.transform.Find("LeftLeg");
			_rightLeg = gameObject.transform.Find("RightLeg");
			_pelvis = gameObject.transform.Find("Pelvis");

			UpdateVrikReferences();
		}

		private void UpdateVrikReferences()
		{
			Plugin.Logger.Info("Tracking device change detected, updating VRIK references");

			if (_trackedDevices.LeftFoot.Found)
			{
				_vrik.solver.leftLeg.positionWeight = _ikManagerAdvanced.LeftLeg_positionWeight;
				_vrik.solver.leftLeg.rotationWeight = _ikManagerAdvanced.LeftLeg_rotationWeight;
			}
			else
			{
				_vrik.solver.leftLeg.positionWeight = 0;
				_vrik.solver.leftLeg.rotationWeight = 0;
			}

			if (_trackedDevices.RightFoot.Found)
			{
				_vrik.solver.rightLeg.positionWeight = _ikManagerAdvanced.RightLeg_positionWeight;
				_vrik.solver.rightLeg.rotationWeight = _ikManagerAdvanced.RightLeg_positionWeight;
			}
			else
			{
				_vrik.solver.rightLeg.positionWeight = 0;
				_vrik.solver.rightLeg.rotationWeight = 0;
			}

			if (_trackedDevices.Waist.Found)
			{
				_vrik.solver.spine.pelvisPositionWeight = _ikManagerAdvanced.Spine_pelvisPositionWeight;
				_vrik.solver.spine.pelvisRotationWeight = _ikManagerAdvanced.Spine_pelvisRotationWeight;
				_vrik.solver.plantFeet = false;
			}
			else
			{
				_vrik.solver.spine.pelvisPositionWeight = 0;
				_vrik.solver.spine.pelvisRotationWeight = 0;
				_vrik.solver.plantFeet = true;
			}
		}

		private void LateUpdate()
		{
			try
			{
				TrackedDeviceState headPosRot = _trackedDevices.Head;
				TrackedDeviceState leftPosRot = _trackedDevices.LeftHand;
				TrackedDeviceState rightPosRot = _trackedDevices.RightHand;

				if (headPosRot.NodeState.tracked)
				{
					_head.position = headPosRot.Position;
					_head.rotation = headPosRot.Rotation;
				}

				if (leftPosRot.NodeState.tracked)
				{
					_leftHand.position = leftPosRot.Position;
					_leftHand.rotation = leftPosRot.Rotation;

					_vrPlatformHelper.AdjustPlatformSpecificControllerTransform(_leftHand);
				}

				if (rightPosRot.NodeState.tracked)
				{
					_rightHand.position = rightPosRot.Position;
					_rightHand.rotation = rightPosRot.Rotation;

					_vrPlatformHelper.AdjustPlatformSpecificControllerTransform(_rightHand);
				}

				if (_leftLeg != null && _trackedDevices.LeftFoot.NodeState.tracked)
				{
					var leftLegPosRot = _trackedDevices.LeftFoot;
					var correction = LeftLegCorrection ?? default;

					_prevLeftLegPos = Vector3.Lerp(_prevLeftLegPos, leftLegPosRot.Position + correction.Position, 15 * Time.deltaTime);
					_prevLeftLegRot = Quaternion.Slerp(_prevLeftLegRot, leftLegPosRot.Rotation * correction.Rotation, 10 * Time.deltaTime);
					_leftLeg.position = _prevLeftLegPos;
					_leftLeg.rotation = _prevLeftLegRot;
				}

				if (_rightLeg != null && _trackedDevices.RightFoot.NodeState.tracked)
				{
					var rightLegPosRot = _trackedDevices.RightFoot;
					var correction = RightLegCorrection ?? default;

					_prevRightLegPos = Vector3.Lerp(_prevRightLegPos, rightLegPosRot.Position + correction.Position, 15 * Time.deltaTime);
					_prevRightLegRot = Quaternion.Slerp(_prevRightLegRot, rightLegPosRot.Rotation * correction.Rotation, 10 * Time.deltaTime);
					_rightLeg.position = _prevRightLegPos;
					_rightLeg.rotation = _prevRightLegRot;
				}

				if (_pelvis != null && _trackedDevices.Waist.NodeState.tracked)
				{
					var pelvisPosRot = _trackedDevices.Waist;
					var correction = PelvisCorrection ?? default;

					_prevPelvisPos = Vector3.Lerp(_prevPelvisPos, pelvisPosRot.Position + correction.Position, 17 * Time.deltaTime);
					_prevPelvisRot = Quaternion.Slerp(_prevPelvisRot, pelvisPosRot.Rotation * correction.Rotation, 13 * Time.deltaTime);
					_pelvis.position = _prevPelvisPos;
					_pelvis.rotation = _prevPelvisRot;
				}

				if (_body == null) return;
				_body.position = _head.position - (_head.transform.up * 0.1f);

				var vel = new Vector3(_body.transform.localPosition.x - _prevBodyPos.x, 0.0f,
					_body.localPosition.z - _prevBodyPos.z);

				var rot = Quaternion.Euler(0.0f, _head.localEulerAngles.y, 0.0f);
				var tiltAxis = Vector3.Cross(gameObject.transform.up, vel);
				_body.localRotation = Quaternion.Lerp(_body.localRotation,
					Quaternion.AngleAxis(vel.magnitude * 1250.0f, tiltAxis) * rot,
					Time.deltaTime * 10.0f);

				_prevBodyPos = _body.transform.localPosition;
			} catch(Exception e)
			{
				Plugin.Logger.Error($"{e.Message}\n{e.StackTrace}");
			}
		}

		private Transform GetHeadTransform()
		{
			var descriptor = GetComponent<AvatarDescriptor>();
			if (descriptor != null)
			{
				//if (descriptor.ViewPoint != null) return descriptor.ViewPoint;
			}

			return gameObject.transform.Find("Head");
		}
	}
}
