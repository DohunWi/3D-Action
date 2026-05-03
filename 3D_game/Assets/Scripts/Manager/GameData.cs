using System;

[Serializable] 
public class GameData
{
    // [기본 정보]
    public int level;
    public int currentExp;
    public int memory; // 

    // [성장 스탯 - SO 기본값 대비 추가된 성장치만 저장]
    public int sanityGrowth;
    public int awarenessGrowth;
    public int tenacityGrowth;
    public int convictionGrowth;
    public int insightGrowth;

    // [위치 정보]
    public string sceneName;
    public float posX, posY, posZ;

    // [아이템]
    public int currentPotions;
}