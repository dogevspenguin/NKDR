using System.Collections.Generic;
using UnityEngine;

using BDArmory.Control;
using BDArmory.Weapons.Missiles;
using BDArmory.Weapons;

namespace BDArmory.Radar
{
    public struct ViewScanResults
    {
        #region Missiles
        public bool foundMissile;
        public bool foundHeatMissile;
        public bool foundRadarMissile;
        public bool foundAntiRadiationMissile;
        public bool foundAGM;
        public bool foundTorpedo;
        public List<IncomingMissile> incomingMissiles; // List of incoming missiles sorted by distance.
        #endregion

        #region Guns
        public bool firingAtMe;
        public float missDistance;
        public float missDeviation;
        public Vector3 threatPosition;
        public Vessel threatVessel;
        public MissileFire threatWeaponManager;
        #endregion
    }

    public struct IncomingMissile
    {
        public MissileBase.TargetingModes guidanceType; // Missile guidance type
        public float distance; // Missile distance
        public float time; // Time to CPA
        public Vector3 position; // Missile position
        public Vessel vessel; // Missile vessel
        public MissileFire weaponManager; // WM of source vessel for regular missiles or WM of missile for modular missiles.
    }
}
