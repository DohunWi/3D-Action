using UnityEngine;
using Unity.Cinemachine;

[CreateAssetMenu(fileName = "New Skill Data", menuName = "Scriptable Object/Skill Data")]
public class SkillData : ScriptableObject
{
    [Header("Info")]
    public string skillName;
    [TextArea] public string description;

    [Header("Animation")]
    public string animTriggerName;

    [Header("Stats")]
    public float damage = 30.0f;
    public float composureDamage = 50.0f;
    public float lucidityCost = 50.0f;
    public float cooldown = 5.0f;
    public float impactRadius = 3.0f; // 범위 공격 반경

    [Header("Audio")]
    public AudioClip castSound;   // 스킬 시전 소리 (기합 등)
    public AudioClip impactSound; // 스킬 폭발/타격 소리

    [Header("VFX")]
    public GameObject castVFX;   // 시전 시 이펙트 (손에서 불빛 등)
    public GameObject impactVFX; // 타격 시 이펙트 (폭발)

    [Header("Camera Shake (Impulse)")]
    // 에셋으로 만든 쉐이크 데이터를 직접 할당
    public CinemachineImpulseDefinition impulseDefinition;
    
    // 필요하다면 아이콘 등도 추가 가능
    // public Sprite icon; 
}