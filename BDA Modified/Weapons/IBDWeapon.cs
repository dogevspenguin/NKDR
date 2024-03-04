namespace BDArmory.Weapons
{
    public interface IBDWeapon
    {
        WeaponClasses GetWeaponClass();

        string GetShortName();

        string GetSubLabel();

        float GetEngageRange();

        string GetMissileType();

        string GetPartName();

        Part GetPart();

        ModuleWeapon GetWeaponModule();

        // extensions for feature_engagementenvelope
    }

    // extensions for feature_engagementenvelope

    public enum WeaponClasses
    {
        Missile,
        Bomb,
        Gun,
        Rocket,
        DefenseLaser,
        SLW
    }
}
