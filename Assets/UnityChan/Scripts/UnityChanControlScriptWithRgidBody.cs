﻿//
// Mecanimのアニメーションデータが、原点で移動しない場合の Rigidbody付きコントローラ
// サンプル
// 2014/03/13 N.Kobyasahi
//
using UnityEngine;
using System.Collections;

// 必要なコンポーネントの列記
[RequireComponent(typeof (Animator))]
[RequireComponent(typeof (CapsuleCollider))]
[RequireComponent(typeof (Rigidbody))]

public class UnityChanControlScriptWithRgidBody : MonoBehaviour
{
	public Transform mainCamera;					//Cardboard Camera
	public float animSpeed = 1.5f;				// アニメーション再生速度設定
	public float lookSmoother = 3.0f;			// a smoothing setting for camera motion
	public bool useCurves = true;				// Mecanimでカーブ調整を使うか設定する
												// このスイッチが入っていないとカーブは使われない
	public float useCurvesHeight = 0.5f;		// カーブ補正の有効高さ（地面をすり抜けやすい時には大きくする）

	// 以下キャラクターコントローラ用パラメタ
	// 前進速度
	public float forwardSpeed = 7.0f;
	// 後退速度
	public float backwardSpeed = 2.0f;
	// 旋回速度
	public float sideSpeed = 4.0f;
	// ジャンプ威力
	public float jumpPower = 3.0f; 
	// キャラクターコントローラ（カプセルコライダ）の参照
	private CapsuleCollider col;
	private Rigidbody rb;
	// キャラクターコントローラ（カプセルコライダ）の移動量
	private Vector3 velocity;
	private Vector3 sideVelocity;

	// CapsuleColliderで設定されているコライダのHeiht、Centerの初期値を収める変数
	private float orgColHight;
	private Vector3 orgVectColCenter;
	//Camera offset from head;
	private Vector3 cameraOffset;
	
	private Animator anim;							// キャラにアタッチされるアニメーターへの参照
	private AnimatorStateInfo currentBaseState;			// base layerで使われる、アニメーターの現在の状態の参照

	private GameObject cameraObject;	// メインカメラへの参照
		
// アニメーター各ステートへの参照
	static int idleState = Animator.StringToHash("Base Layer.Idle");
	static int locoState = Animator.StringToHash("Base Layer.Locomotion");
	static int jumpState = Animator.StringToHash("Base Layer.Jump");
	static int restState = Animator.StringToHash("Base Layer.Rest");

// 初期化
	void Start ()
	{
		// Animatorコンポーネントを取得する
		anim = GetComponent<Animator>();
		// CapsuleColliderコンポーネントを取得する（カプセル型コリジョン）
		col = GetComponent<CapsuleCollider>();
		rb = GetComponent<Rigidbody>();
		//メインカメラを取得する
		cameraObject = GameObject.FindWithTag("MainCamera");
		// CapsuleColliderコンポーネントのHeight、Centerの初期値を保存する
		orgColHight = col.height;
		orgVectColCenter = col.center;
		cameraOffset = mainCamera.position - transform.position;
}
	
	
// 以下、メイン処理.リジッドボディと絡めるので、FixedUpdate内で処理を行う.
	void FixedUpdate ()
	{
		float h = Input.GetAxis("Horizontal");				// 入力デバイスの水平軸をhで定義
		float v = Input.GetAxis("Vertical");				// 入力デバイスの垂直軸をvで定義
		float a = Input.GetAxis("Fire1");		
		anim.SetFloat("Speed", v);							// Animator側で設定している"Speed"パラメタにvを渡す
		anim.SetFloat("Direction", h); 						// Animator側で設定している"Direction"パラメタにhを渡す
		anim.speed = animSpeed;								// Animatorのモーション再生速度に animSpeedを設定する
		currentBaseState = anim.GetCurrentAnimatorStateInfo(0);	// 参照用のステート変数にBase Layer (0)の現在のステートを設定する


		//for gravity problem
		RaycastHit hit = new RaycastHit();
		if (Physics.Raycast (transform.position, -Vector3.up, out hit)) {
			if (hit.distance >= 0.1) {
				rb.useGravity = false;
				rb.AddForce(Physics.gravity * rb.mass * 20);
			}
		}

		if (a > 0.1) {
			anim.SetBool ("Aim", true);
		} else {
			anim.SetBool ("Aim", false);
		}
		
		// 以下、キャラクターの移動処理
		velocity = new Vector3(0, 0, v);		// 上下のキー入力からZ軸方向の移動量を取得
		// キャラクターのローカル空間での方向に変換
		velocity = transform.TransformDirection(velocity);
		//以下のvの閾値は、Mecanim側のトランジションと一緒に調整する
		if (v > 0.1) {
			if (a > 0.1) {
				velocity *= forwardSpeed/2;
			} else {
				velocity *= forwardSpeed;		// 移動速度を掛ける
			}
		} else if (v < -0.1) {
			if (a > 0.1) {
				velocity *= backwardSpeed/2;
			} else {
				velocity *= backwardSpeed;	// 移動速度を掛ける
			}
		}

		sideVelocity = new Vector3 (h, 0, 0);
		sideVelocity = transform.TransformDirection (sideVelocity);
		if (a > 0.1) {
			sideVelocity *= sideSpeed/2;
		} else {
			sideVelocity *= sideSpeed;
		}
		
		if (Input.GetButtonDown("Jump")) {
			if(!anim.IsInTransition(0)) {
				rb.AddForce(Vector3.up * jumpPower, ForceMode.VelocityChange);
				anim.SetTrigger("JumpTrigger");
			}
		}

		// 上下のキー入力でキャラクターを移動させる
		transform.localPosition += velocity * Time.fixedDeltaTime;

		// 左右のキー入力でキャラクタをY軸で旋回させる
		transform.localPosition += sideVelocity * Time.fixedDeltaTime;

		mainCamera.position = transform.position + cameraOffset;

		// 以下、Animatorの各ステート中での処理
		// Locomotion中
		// 現在のベースレイヤーがlocoStateの時
		if (currentBaseState.fullPathHash == locoState){
			//カーブでコライダ調整をしている時は、念のためにリセットする
			if(useCurves){
				resetCollider();
			}
		}
		// JUMP中の処理
		// 現在のベースレイヤーがjumpStateの時
		else if(currentBaseState.fullPathHash == jumpState)
		{
//			cameraObject.SendMessage("setCameraPositionJumpView");	// ジャンプ中のカメラに変更
			// ステートがトランジション中でない場合
			if(!anim.IsInTransition(0))
			{
				
				// 以下、カーブ調整をする場合の処理
				if(useCurves){
					// 以下JUMP00アニメーションについているカーブJumpHeightとGravityControl
					// JumpHeight:JUMP00でのジャンプの高さ（0〜1）
					// GravityControl:1⇒ジャンプ中（重力無効）、0⇒重力有効
					float jumpHeight = anim.GetFloat("JumpHeight");
					float gravityControl = anim.GetFloat("GravityControl"); 
					if(gravityControl > 0)
						rb.useGravity = false;	//ジャンプ中の重力の影響を切る
										
					// レイキャストをキャラクターのセンターから落とす
					Ray ray = new Ray(transform.position + Vector3.up, -Vector3.up);
					RaycastHit hitInfo = new RaycastHit();
					// 高さが useCurvesHeight 以上ある時のみ、コライダーの高さと中心をJUMP00アニメーションについているカーブで調整する
					if (Physics.Raycast(ray, out hitInfo))
					{
						if (hitInfo.distance > useCurvesHeight)
						{
							col.height = orgColHight - jumpHeight;			// 調整されたコライダーの高さ
							float adjCenterY = orgVectColCenter.y + jumpHeight;
							col.center = new Vector3(0, adjCenterY, 0);	// 調整されたコライダーのセンター
						}
						else{
							// 閾値よりも低い時には初期値に戻す（念のため）					
							resetCollider();
						}
					}
				}
			}
		}
		// IDLE中の処理
		// 現在のベースレイヤーがidleStateの時
		else if (currentBaseState.fullPathHash == idleState)
		{
			//カーブでコライダ調整をしている時は、念のためにリセットする
			if(useCurves){
				resetCollider();
			}
		}
	}

	void OnGUI()
	{
//		GUI.Box(new Rect(Screen.width -260, 10 ,250 ,150), "Interaction");
//		GUI.Label(new Rect(Screen.width -245,30,250,30),"Up/Down Arrow : Go Forwald/Go Back");
//		GUI.Label(new Rect(Screen.width -245,50,250,30),"Left/Right Arrow : Turn Left/Turn Right");
//		GUI.Label(new Rect(Screen.width -245,70,250,30),"Hit Space key while Running : Jump");
//		GUI.Label(new Rect(Screen.width -245,90,250,30),"Hit Spase key while Stopping : Rest");
//		GUI.Label(new Rect(Screen.width -245,110,250,30),"Left Control : Front Camera");
//		GUI.Label(new Rect(Screen.width -245,130,250,30),"Alt : LookAt Camera");
	}


	// キャラクターのコライダーサイズのリセット関数
	void resetCollider()
	{
	// コンポーネントのHeight、Centerの初期値を戻す
		col.height = orgColHight;
		col.center = orgVectColCenter;
	}
}
