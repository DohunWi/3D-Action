using UnityEngine;

[CreateAssetMenu(fileName = "PlayerBaseStats", menuName = "Somnia/Player Base Stats")]
public class PlayerBaseStatsSO : ScriptableObject
{
    [Header("--- 기본 성장 스탯 (뉴게임 시작값) ---")]
    public int sanity     = 10;
    public int awareness  = 10;
    public int tenacity   = 10;
    public int conviction = 10;
    public int insight    = 10;

    [Header("--- 스탯 공식 계수 ---")]
    public float egoPerSanity         = 10f;  // maxEgo      = sanity * egoPerSanity
    public float lucidityPerAwareness = 5f;   // maxLucidity = awareness * lucidityPerAwareness
    public float baseVolition         = 50f;  // maxVolition = baseVolition + tenacity * volitionPerTenacity
    public float volitionPerTenacity  = 3f;
    public float attackPerConviction  = 1.5f; // attackPower = conviction * attackPerConviction
    public float speedPerInsight      = 0.05f;

    [Header("--- 이동 설정 ---")]
    public float baseMoveSpeed   = 4.5f;
    public float baseSprintSpeed = 9.5f;

    [Header("--- 회복 설정 ---")]
    public float volitionRegenRate  = 15f;
    public float volitionRegenDelay = 2f;
    public float lucidityRegenRate  = 5f;

    [Header("--- 강인도 설정 ---")]
    public float maxComposure          = 50f;
    public float composureRecoveryTime = 3f;
    public float composureRecoveryRate = 10f;

    [Header("--- 경험치 ---")]
    public int   startingMaxExp     = 100;
    public float expScalingMultiplier = 1.2f; // 레벨업마다 maxExp *= 이 값
}
