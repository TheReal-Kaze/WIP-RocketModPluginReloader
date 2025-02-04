using HarmonyLib;
using SDG.Framework.Modules;
using SDG.Unturned;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using Component = UnityEngine.Component;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

namespace PatchModule
{
    public class Main : IModuleNexus
    {
        public const string HarmonyId = "com.Kaze.trade";
        public Harmony? harmony;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeConsole();

        public void initialize()
        {

            if (AllocConsole())
            {
                RedirectConsoleIO();
                Console.WriteLine("Console window allocated.");
                Console.WriteLine("Console ready. Type commands below:");
                UnturnedLog.info("Console window allocated.");
            }
            else
            {
                UnturnedLog.info("Failed to allocate console.");
            }

            harmony = new Harmony(HarmonyId);
            harmony.PatchAll();

            var list = Harmony.GetAllPatchedMethods().ToList();
            Console.WriteLine($"Count of patched method {list.Count}");



            PlayerAnimator.OnGestureChanged_Global += OnGestureChanged_Global;
            
        }

        void OnGestureChanged_Global (PlayerAnimator playerAnimator, EPlayerGesture gesture)
        {
            if (gesture != EPlayerGesture.PUNCH_RIGHT) return;
            GetInteractableDoorDetails(playerAnimator.player);

            }
        public void shutdown()
        {
            PlayerAnimator.OnGestureChanged_Global -= OnGestureChanged_Global;
            FreeConsole();
        }

        NpcGlobalEventHook
        public void GetInteractableDoorDetails(Player player)
        {
            Console.WriteLine("Ligma");
            RaycastHit hit;
            Ray ray = new(player.look.aim.position, player.look.aim.forward);
            if (Physics.Raycast(ray, out hit, 3, RayMasks.BARRICADE_INTERACT))
            {
                Transform transform = hit.transform;
                InteractableDoor interactableDoor = hit.transform.GetComponentInParent<InteractableDoor>();
                if (interactableDoor == null) { Console.WriteLine("ID is null"); return; }

                Console.WriteLine("ID is not null aa");

                List<Component> components = hit.transform.GetComponentsInParent<Component>().ToList();

                if (components == null) Console.WriteLine("Null list");
                // Loop through each component and log its type
                //foreach (Component component in components)
                //{
                //    if (!(component is MonoBehaviour)) components.Remove(component);
                //}

                Console.WriteLine(components.Count);
            int i = 0;
                foreach (Component component in components)
                {
                    Console.WriteLine($"Main parent Component {++i}: " + component.GetType().Name );
                }

            }

        }
        private void RedirectConsoleIO()
        {
            // Redirect standard output to the console
            var standardOutput = new StreamWriter(Console.OpenStandardOutput())
            {
                AutoFlush = true
            };
            Console.SetOut(standardOutput);

            // Redirect standard error to the console
            var standardError = new StreamWriter(Console.OpenStandardError())
            {
                AutoFlush = true
            };
            Console.SetError(standardError);

            // Redirect standard input from the console
            var standardInput = new StreamReader(Console.OpenStandardInput());
            Console.SetIn(standardInput);
        }
    }

    [HarmonyPatch]
    public class Patching()
    {
        static Collider[] checkColliders = new Collider[1];
        [HarmonyPatch(typeof(UseableBarricade),"checkClaims")]
        [HarmonyPostfix]
        public static void tickPatch(UseableBarricade __instance)
        {
            Console.WriteLine("Pre Ticking");
            try
            {
                if (__instance == null)
                {
                    Console.WriteLine("Error: __instance is null.");
                    return;
                }

                // Use reflection to get the fields
                var pointInWorldSpaceInfo = __instance.GetType().GetMethod("getPointInWorldSpace", BindingFlags.NonPublic | BindingFlags.Instance);

                var boundsRotationField = __instance.GetType().GetField("boundsRotation", BindingFlags.NonPublic | BindingFlags.Instance);
                var boundsCenterField = __instance.GetType().GetField("boundsCenter", BindingFlags.NonPublic | BindingFlags.Instance);
                var boundsExtentsField = __instance.GetType().GetField("boundsExtents", BindingFlags.NonPublic | BindingFlags.Instance);

                if (pointInWorldSpaceInfo == null || boundsRotationField == null || boundsCenterField == null || boundsExtentsField == null )
                {
                    Console.WriteLine("Error: One or more fields are not found.");
                    return;
                }

                // Get the values from the fields
                Vector3 pointInWorldSpace = (Vector3)pointInWorldSpaceInfo.Invoke(__instance, null);
                Quaternion boundsRotation = (Quaternion)boundsRotationField.GetValue(__instance);
                Vector3 boundsCenter = (Vector3)boundsCenterField.GetValue(__instance);
                Vector3 boundsExtents = (Vector3)boundsExtentsField.GetValue(__instance);

                Vector3 halfExtents = boundsExtents;
                halfExtents.x -= 0.25f;
                halfExtents.y -= 0.5f;
                halfExtents.z += 0.6f;

                if (checkColliders == null)
                {
                    Console.WriteLine("Error: checkColliders is null.");
                    return;
                }

                Console.WriteLine($"pointInWorldSpace = {pointInWorldSpace}, boundsRotation = {boundsRotation}, boundsCenter = {boundsCenter}, halfExtents = {halfExtents}, checkColliders count = {checkColliders.Length}");

                int OverlapBoxNonAlloc = Physics.OverlapBoxNonAlloc(pointInWorldSpace + boundsRotation * boundsCenter, halfExtents, checkColliders, boundsRotation, RayMasks.BLOCK_DOOR_OPENING);
                Console.WriteLine("K_The number is: " + OverlapBoxNonAlloc);


                GameObject barricade = GameObject.CreatePrimitive(PrimitiveType.Cube);

                Vector3 cubePosition = pointInWorldSpace + boundsRotation * boundsCenter;
                barricade.transform.position = cubePosition;

                barricade.transform.rotation = boundsRotation;

                barricade.transform.localScale = halfExtents * 2; 

                barricade.tag = "Barricade";

                barricade.layer = RayMasks.BARRICADE;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }


            Console.WriteLine("Post Ticking");
        }
    }
    
}
