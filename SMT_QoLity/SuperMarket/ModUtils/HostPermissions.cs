using Damntry.UtilsUnity.ExtensionMethods;
using System;

namespace SuperQoLity.SuperMarket.ModUtils {

    public enum PlayerPermissionsEnum {
        None,
        /// <summary>
        /// Basic permission to be able to interact with anything in the world.
        /// </summary>
        General,
        /// <summary>
        /// Employee management, building and decorating, breaking down pillars...
        /// </summary>
        Manager,
        /// <summary>
        /// Cash register.
        /// </summary>
        Cashier,
        /// <summary>
        /// Place and remove producsts and boxes from shelves.
        /// </summary>
        Restocker,
        /// <summary>
        /// Permission to carry brooms and use them.
        /// </summary>
        Security,
        /* Other permissions referenced but not changeable.
        TP - Teleport
        JP - Jail
        */
    }

    public static class HostPermissions {

        private static PlayerPermissions playerPerms;

        private static PlayerPermissions GetCachedPlayerPermissions() =>
            UnityObjectExtensions.AssignIfDeadReference(ref playerPerms, SMTInstances.PlayerPermissions());

        public static bool HasPermission(PlayerPermissionsEnum permission) {
            if (!GetCachedPlayerPermissions()) {
                return false;
            }

            return permission switch {
                PlayerPermissionsEnum.General => GetCachedPlayerPermissions().hasGP,
                PlayerPermissionsEnum.Manager => GetCachedPlayerPermissions().hasMP,
                PlayerPermissionsEnum.Cashier => GetCachedPlayerPermissions().hasCP,
                PlayerPermissionsEnum.Restocker => GetCachedPlayerPermissions().hasRP,
                PlayerPermissionsEnum.Security => GetCachedPlayerPermissions().hasSP,
                PlayerPermissionsEnum.None => true,
                _ => throw new NotSupportedException(permission.ToString())
            };
        }
    }
}
