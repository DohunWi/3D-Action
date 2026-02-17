using System;

[Serializable] 
public class GameData
{
    // [기본 정보]
    public int level;
    public int currentExp;
    public int memory; // 

    // [성장 스탯 - 세계관 용어]
    public int sanity;     // Vigor
    public int awareness;  // Mind
    public int tenacity;   // Endurance
    public int conviction; // Strength
    public int insight;    // Dexterity

    // [위치 정보]
    public string sceneName;
    public float posX, posY, posZ;

    // [아이템]
    public int currentPotions;
}