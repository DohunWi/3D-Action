using UnityEngine;
using System.Collections.Generic;

// 프로젝트 창에서 우클릭 -> Create -> Weapon Data 로 생성 가능하게 만듦
[CreateAssetMenu(fileName = "New Weapon", menuName = "Scriptable Object/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("Info")]
    public string weaponName;
    public string description;

    [Header("Stats")]
    public float damage = 10f;
    public float attackRange = 1.5f;

    [Header("Combo Settings")]
    // ★ 콤보 단계별 데미지 배율 (인스펙터에서 설정)
    // 예: [1.0, 1.2, 1.5, 2.0]
    public List<float> comboMultipliers = new List<float>();

    [Header("Audio")]
    public AudioClip swingSound; // 휘두르는 소리
    public AudioClip hitSound;   // 적중했을 때 소리
    public AudioClip criticalHitSound;  // 치명타 소리
    
    [Header("VFX")]
    public GameObject hitVFX;    // 피 튀기는 이펙트

    [Header("Player Only")]
    public float cameraShake = 0.1f; // 플레이어 무기일 때만 쓸 값
}