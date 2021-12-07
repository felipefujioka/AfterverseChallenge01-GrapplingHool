using System.IO.MemoryMappedFiles;
using DG.Tweening;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
using UnityEngine.InputSystem;
#endif

/* Note: animations are called via the controller for both the character and capsule using animator null checks
 */

namespace StarterAssets
{
	[RequireComponent(typeof(CharacterController))]
#if ENABLE_INPUT_SYSTEM && STARTER_ASSETS_PACKAGES_CHECKED
	[RequireComponent(typeof(PlayerInput))]
#endif
	public class ThirdPersonController : MonoBehaviour
	{
		[Header("Player")]
		[Tooltip("Move speed of the character in m/s")]
		public float MoveSpeed = 2.0f;
		[Tooltip("Sprint speed of the character in m/s")]
		public float SprintSpeed = 5.335f;
		[Tooltip("How fast the character turns to face movement direction")]
		[Range(0.0f, 0.3f)]
		public float RotationSmoothTime = 0.12f;
		[Tooltip("Acceleration and deceleration")]
		public float SpeedChangeRate = 10.0f;

		[Space(10)]
		[Tooltip("The height the player can jump")]
		public float JumpHeight = 1.2f;
		[Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
		public float Gravity = -15.0f;

		[Space(10)]
		[Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
		public float JumpTimeout = 0.50f;
		[Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
		public float FallTimeout = 0.15f;

		[Header("Player Grounded")]
		[Tooltip("If the character is grounded or not. Not part of the CharacterController built in grounded check")]
		public bool Grounded = true;
		[Tooltip("Useful for rough ground")]
		public float GroundedOffset = -0.14f;
		[Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
		public float GroundedRadius = 0.28f;
		[Tooltip("What layers the character uses as ground")]
		public LayerMask GroundLayers;

		[Header("Cinemachine")]
		[Tooltip("The follow target set in the Cinemachine Virtual Camera that the camera will follow")]
		public GameObject CinemachineCameraTarget;
		[Tooltip("How far in degrees can you move the camera up")]
		public float TopClamp = 70.0f;
		[Tooltip("How far in degrees can you move the camera down")]
		public float BottomClamp = -30.0f;
		[Tooltip("Additional degress to override the camera. Useful for fine tuning camera position when locked")]
		public float CameraAngleOverride = 0.0f;
		[Tooltip("For locking the camera position on all axis")]
		public bool LockCameraPosition = false;

		[Header("Grappling Hook")] 
		public Transform RightHand;
		public Rigidbody RigidBody;
		public LineRenderer LineRenderer;
		public Rope RopePrefab;
		
		// cinemachine
		private float _cinemachineTargetYaw;
		private float _cinemachineTargetPitch;

		// player
		private float _speed;
		private float _animationBlend;
		private float _targetRotation = 0.0f;
		private float _rotationVelocity;
		private float _verticalVelocity;
		private float _terminalVelocity = 53.0f;

		// timeout deltatime
		private float _jumpTimeoutDelta;
		private float _fallTimeoutDelta;

		// animation IDs
		private int _animIDSpeed;
		private int _animIDGrounded;
		private int _animIDJump;
		private int _animIDFreeFall;
		private int _animIDMotionSpeed;
		private int _animIDHanging;
		private int _animIDHooked;

		[SerializeField] private Animator _animator;
		private CharacterController _controller;
		private StarterAssetsInputs _input;
		private GameObject _mainCamera;

		private const float _threshold = 0.01f;

		private bool _hasAnimator;

		private ConfigurableJoint _currenJoint;
		private Tween _currentTween;

		public bool Hooked;
		public bool Hanging;
		public bool JustTouchedGround;
		private Vector3 _hookAnchor;
		private Vector3 _hookNormal;

		private void Awake()
		{
			// get a reference to our main camera
			if (_mainCamera == null)
			{
				_mainCamera = GameObject.FindGameObjectWithTag("MainCamera");
			}
		}

		private void Start()
		{
			_hasAnimator = _animator != null;
			_controller = GetComponent<CharacterController>();
			_input = GetComponent<StarterAssetsInputs>();

			AssignAnimationIDs();

			// reset our timeouts on start
			_jumpTimeoutDelta = JumpTimeout;
			_fallTimeoutDelta = FallTimeout;
		}

		private void Update()
		{
			if (!Hooked && !Hanging)
			{
				JumpAndGravity();
				Move();
			}

			if (!Hanging)
			{
				GroundedCheck();	
			}
			
			if (Hanging || Hooked)
			{
				WallJump();
			}

			if (Grounded && !Hanging)
			{
				var transformRotation = transform.rotation;
				transformRotation.eulerAngles = new Vector3(0, transformRotation.eulerAngles.y, 0);
				transform.rotation = transformRotation;
			}
			
			SetSwinging(!Grounded && Hooked);
			
		}
		
		private void LateUpdate()
		{
			CameraRotation();
			
			if (Hooked)
			{
				DrawRope();
			}
		}

		private void SetSwinging(bool isSwinging)
		{
			RigidBody.isKinematic = !isSwinging;
			RigidBody.useGravity = isSwinging;
			RigidBody.constraints = isSwinging ? RigidbodyConstraints.None : RigidbodyConstraints.FreezeRotation;

			if (JustTouchedGround)
			{
				Hooked = isSwinging;
				_controller.enabled = !isSwinging;
				_animator.SetBool(_animIDHanging, Hanging);
				_animator.SetBool(_animIDHooked, Hooked);
				LineRenderer.enabled = isSwinging;
			}
		}

		private void WallJump()
		{
			if (_input.jump)
			{
				_input.jump = false;
				Hanging = false;
				Hooked = false;
				_controller.enabled = true;
				_animator.SetBool(_animIDHanging, false);
				_animator.SetBool(_animIDHooked, false);
				LineRenderer.enabled = false;
			}
		}


		private void DrawRope()
		{
			LineRenderer.enabled = true;
			LineRenderer.startWidth = 0.1f;
			LineRenderer.endWidth = 0.1f;
			LineRenderer.startColor = Color.red;
			LineRenderer.endColor = Color.magenta;
			LineRenderer.SetPositions(new Vector3[]{RightHand.transform.position, _hookAnchor});
		}

		private void AssignAnimationIDs()
		{
			_animIDSpeed = Animator.StringToHash("Speed");
			_animIDGrounded = Animator.StringToHash("Grounded");
			_animIDJump = Animator.StringToHash("Jump");
			_animIDFreeFall = Animator.StringToHash("FreeFall");
			_animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
			_animIDHanging = Animator.StringToHash("Hanging");
			_animIDHooked = Animator.StringToHash("Hooked");
		}

		private void GroundedCheck()
		{
			// set sphere position, with offset
			Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
			var newValue = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);

			JustTouchedGround = newValue ^ Grounded;
			Grounded = newValue;
			
			// update animator if using character
			if (_hasAnimator)
			{
				_animator.SetBool(_animIDGrounded, Grounded);
			}
		}

		private void CameraRotation()
		{
			// if there is an input and camera position is not fixed
			if (_input.look.sqrMagnitude >= _threshold && !LockCameraPosition)
			{
				_cinemachineTargetYaw += _input.look.x * Time.deltaTime;
				_cinemachineTargetPitch += _input.look.y * Time.deltaTime;
			}

			// clamp our rotations so our values are limited 360 degrees
			_cinemachineTargetYaw = ClampAngle(_cinemachineTargetYaw, float.MinValue, float.MaxValue);
			if (!Hooked)
			{
				_cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, BottomClamp, TopClamp);	
			}

			// Cinemachine will follow this target
			CinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch + CameraAngleOverride, _cinemachineTargetYaw, 0.0f);
		}

		private void Move()
		{
			// set target speed based on move speed, sprint speed and if sprint is pressed
			float targetSpeed = _input.sprint ? SprintSpeed : MoveSpeed;

			// a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

			// note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is no input, set the target speed to 0
			if (_input.move == Vector2.zero) targetSpeed = 0.0f;

			// a reference to the players current horizontal velocity
			float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

			float speedOffset = 0.1f;
			float inputMagnitude = _input.analogMovement ? _input.move.magnitude : 1f;

			// accelerate or decelerate to target speed
			if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
			{
				// creates curved result rather than a linear one giving a more organic speed change
				// note T in Lerp is clamped, so we don't need to clamp our speed
				_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude, Time.deltaTime * SpeedChangeRate);

				// round speed to 3 decimal places
				_speed = Mathf.Round(_speed * 1000f) / 1000f;
			}
			else
			{
				_speed = targetSpeed;
			}
			_animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);

			// normalise input direction
			Vector3 inputDirection = new Vector3(_input.move.x, 0.0f, _input.move.y).normalized;

			// note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
			// if there is a move input rotate player when the player is moving
			if (_input.move != Vector2.zero)
			{
				_targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + _mainCamera.transform.eulerAngles.y;
				float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, RotationSmoothTime);

				// rotate to face input direction relative to camera position
				transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
			}


			Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

			// move the player
			_controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) + new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

			// update animator if using character
			if (_hasAnimator)
			{
				_animator.SetFloat(_animIDSpeed, _animationBlend);
				_animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
			}
		}

		private void JumpAndGravity()
		{
			if (Grounded)
			{
				// reset the fall timeout timer
				_fallTimeoutDelta = FallTimeout;

				// update animator if using character
				if (_hasAnimator)
				{
					_animator.SetBool(_animIDJump, false);
					_animator.SetBool(_animIDFreeFall, false);
				}

				// stop our velocity dropping infinitely when grounded
				if (_verticalVelocity < 0.0f)
				{
					_verticalVelocity = -2f;
				}

				// Jump
				if (_input.jump && _jumpTimeoutDelta <= 0.0f)
				{
					// the square root of H * -2 * G = how much velocity needed to reach desired height
					_verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

					// update animator if using character
					if (_hasAnimator)
					{
						_animator.SetBool(_animIDJump, true);
					}
				}

				// jump timeout
				if (_jumpTimeoutDelta >= 0.0f)
				{
					_jumpTimeoutDelta -= Time.deltaTime;
				}
			}
			else
			{
				// reset the jump timeout timer
				_jumpTimeoutDelta = JumpTimeout;

				// fall timeout
				if (_fallTimeoutDelta >= 0.0f)
				{
					_fallTimeoutDelta -= Time.deltaTime;
				}
				else
				{
					// update animator if using character
					if (_hasAnimator)
					{
						_animator.SetBool(_animIDFreeFall, true);
					}
				}

				// if we are not grounded, do not jump
				_input.jump = false;
			}

			// apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
			if (_verticalVelocity < _terminalVelocity)
			{
				_verticalVelocity += Gravity * Time.deltaTime;
			}
		}

		private static float ClampAngle(float lfAngle, float lfMin, float lfMax)
		{
			if (lfAngle < -360f) lfAngle += 360f;
			if (lfAngle > 360f) lfAngle -= 360f;
			return Mathf.Clamp(lfAngle, lfMin, lfMax);
		}

		private void OnDrawGizmosSelected()
		{
			Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
			Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

			if (Grounded) Gizmos.color = transparentGreen;
			else Gizmos.color = transparentRed;
			
			// when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
			Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z), GroundedRadius);
		}

		public void HookTo(Vector3 hitPoint, Vector3 hitNormal)
		{

			if (_currenJoint != null)
			{
				Destroy(_currenJoint.gameObject);
			}

			if (_currentTween != null && _currentTween.IsPlaying())
			{
				_currentTween.Kill(false);
			}

			var rope = Instantiate(RopePrefab);
			ConfigurableJoint originJoint = rope.OriginJoint;
			ConfigurableJoint handJoint = rope.HandJoint;
			originJoint.transform.position = hitPoint;
			handJoint.connectedBody = RigidBody;
			handJoint.transform.position = RightHand.position;

			var ropeDirection = hitPoint - RightHand.position;
			var axis = Vector3.Cross(ropeDirection, hitNormal);

			originJoint.axis = axis;
			originJoint.secondaryAxis = hitNormal;

			originJoint.linearLimit = new SoftJointLimit()
			{
				limit = (hitPoint - transform.position).magnitude,
				bounciness = 0,
				contactDistance = 0
			};

			_currenJoint = originJoint;
			Hooked = true;
			Hanging = false;
			_hookAnchor = hitPoint;
			_hookNormal = hitNormal;

			_controller.enabled = false;
			
			_animator.SetBool(_animIDHooked, true);
			_animator.SetBool(_animIDHanging, false);
		}

		private bool IsParallel(Vector3 a, Vector3 b)
		{
			return Mathf.Approximately(Vector3.Dot(a.normalized, b.normalized), 1);
		}

		private bool IsHorizontal(Vector3 a)
		{
			return Mathf.Approximately(a.y, 0);
		}

		public void RetractRope()
		{
			if (Hooked)
			{
				if (_currenJoint != null)
				{
					Destroy(_currenJoint.gameObject);
				}
				
				Tween moveTween = transform.DOMove(_hookAnchor + _hookNormal * 0.2f, 30).SetSpeedBased(true);
				moveTween.onComplete += () =>
				{
					if (IsParallel(_hookNormal, Vector3.up))
					{
						Hanging = false;
						Hooked = false;
						_animator.SetBool(_animIDHanging, false);
						_animator.SetBool(_animIDFreeFall, false);
						_controller.enabled = true;
					}
					else
					{
						Hanging = true;
						Hooked = false;
						_animator.SetBool(_animIDHanging, true);
						_animator.SetBool(_animIDFreeFall, false);
						transform.LookAt(_hookAnchor - _hookNormal, Vector3.up);	
					}
					
					LineRenderer.enabled = false;
				};

				transform.LookAt(_hookAnchor, Vector3.up);
				
				moveTween.Play();
				
				_animator.SetBool(_animIDHanging, false);
				_animator.SetBool(_animIDFreeFall, true);
				_animator.SetBool(_animIDHooked, false);

				_currentTween = moveTween;
				
				transform.eulerAngles = Vector3.zero;
				var lookAt = _hookAnchor;
				lookAt.y = transform.position.y;
				transform.LookAt(lookAt);
			}
		}
	}
}