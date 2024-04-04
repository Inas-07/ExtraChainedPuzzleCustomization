using ChainedPuzzles;
using Il2CppInterop.Runtime.Injection;
using Player;
using ScanPosOverride.Managers;
using ScanPosOverride.PuzzleOverrideData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;
using static RootMotion.FinalIK.RagdollUtility;

namespace ScanPosOverride.Component
{
    internal class ConcurrentClusterHud: MonoBehaviour
    {
        internal CP_Cluster_Core parent { get; set; }

        internal CP_Bioscan_Hud parentHud { get; set; }

        internal PuzzleOverride def { get; set; }

        //private string text = string.Empty;

        private StringBuilder displayText = new();

        private bool m_Setup = false;
        
        public PlayerCountRequirement playerCountReq { get; private set; } = PlayerCountRequirement.INVALID;

        private List<CP_Bioscan_Core> children;

        private float childRadius = -1.0f;
        private int[] playerInScanCount;

        private readonly string[] ROMAN = { "I", "II", "III", "IV" };

        public enum PlayerCountRequirement
        {
            INVALID,
            ANY,
            SOLO,
            DUO
        }

        /// <summary>
        /// evaluate required number of player for this scan
        /// </summary>
        /// <param name="child"></param>
        /// <returns>
        /// </returns>
        private PlayerCountRequirement ReqPlayerCount(iChainedPuzzleCore child)
        {
            var core = child.Cast<CP_Bioscan_Core>();
            var scanner = core.m_playerScanner.Cast<CP_PlayerScanner>();

            var playerRequirement = scanner.m_playerRequirement;

            var scanSpeed = scanner.m_scanSpeeds;
            
            switch (playerRequirement)
            {
                case PlayerRequirement.Solo: 
                    return scanSpeed[0] > 0.0f ? PlayerCountRequirement.SOLO : PlayerCountRequirement.INVALID;
                
                case PlayerRequirement.None:
                    int i = 0; // index pointing to the first non-zero scanspeed
                    while (i < scanSpeed.Count && scanSpeed[i] == 0.0f) i++; // this float equality compare should be fine

                    if (i >= 2)
                    {
                        return PlayerCountRequirement.INVALID; // require at least 3 players, impossible for concurrent cluster
                    }
                    else if (i == 1)
                    {
                        return PlayerCountRequirement.DUO;  // for i >= 2, even if scanspeed[i] > 0.0f, this is still a duo-concurrent-cluster scan
                    }
                    else // i == 0
                    {
                        // NOTE: there could be even more complicated case,
                        // for example, the scan require either 1 or 3 players, but reject 2 players.
                        // but this is really rare case so I don't bother to implement it for now
                        return scanSpeed[i + 1] > 0.0f ? PlayerCountRequirement.ANY : PlayerCountRequirement.SOLO;
                    }

                case PlayerRequirement.All:
                default:
                    return PlayerCountRequirement.INVALID;
            }
        }

        internal bool Setup()
        {
            if(parent.m_childCores.Count < 2 || parent.m_childCores.Count > 4)
            {
                SPOLogger.Error($"ConcurrentClusterHud: got cluster scan with {parent.m_childCores.Count} children, which is invalid for concurrent cluster");
                return false;
            }

            playerCountReq = ReqPlayerCount(parent.m_childCores[0]); // assume every child scan is identical
            if(playerCountReq == PlayerCountRequirement.INVALID)
            {
                SPOLogger.Error($"ConcurrentCluster setup: playerRequirement is {PlayerRequirement.All}, which is invalid for concurrent cluster");
                return false;
            }

            switch (parent.m_childCores.Count)
            {
                case 2:
                    switch (playerCountReq) 
                    {
                        case PlayerCountRequirement.ANY:
                        case PlayerCountRequirement.SOLO:
                            //text = "[{}] | [{}]";
                            m_Setup = true;     
                            break;
                        
                        case PlayerCountRequirement.DUO:     
                            //text = "[{},{}] | [{},{}]";     
                            m_Setup = true;     
                            break;
                    }

                    break;

                case 4:
                    switch (playerCountReq)
                    {
                        case PlayerCountRequirement.ANY:
                        case PlayerCountRequirement.SOLO: 
                            //text = "[{}] | [{}] | [{}] | [{}]"; 
                            m_Setup = true;     
                            break;
                    }

                    break;

                case 3:
                    switch (playerCountReq)
                    {
                        case PlayerCountRequirement.ANY:
                        case PlayerCountRequirement.SOLO: 
                            //text = "[{}] | [{}] | [{}]";
                            m_Setup = true;
                            break;
                    }

                    break;
            }

            if (!m_Setup)
            {
                SPOLogger.Error($"ConcurrentCluster setup: Something went wrong! PlayerCountRequirement: {playerCountReq}, children num: {parent.m_childCores.Count}");
                return false;
            }

            playerInScanCount = new int[parent.m_childCores.Count];
            Array.Fill(playerInScanCount, 0);

            children = parent.m_childCores.ToList().ConvertAll(c => c.Cast<CP_Bioscan_Core>());
            childRadius = parent.m_childCores[0].Cast<CP_Bioscan_Core>().m_playerScanner.Cast<CP_PlayerScanner>().Radius;

            return true;
        }

        void LateUpdate()
        {
            if (!m_Setup || !parentHud.m_visible || !parentHud.m_isClosestToPlayer) return;

            displayText.Clear();
            displayText.AppendLine().Append("<color=white>");

            for(int i = 0; i < children.Count; i++)
            {
                int cnt = 0;
                var child = children[i];
                foreach(var p in PlayerManager.PlayerAgentsInLevel)
                {
                    if ((p.Position - child.m_position).magnitude < childRadius)
                    {
                        cnt += 1;
                    }
                }

                playerInScanCount[i] = cnt;

                displayText.Append(ROMAN[i]).Append(": ");
                displayText.Append("[");
                switch(playerCountReq)
                {
                    case PlayerCountRequirement.ANY: 
                        for(int n = 0; n < cnt; n++)
                        {
                            displayText.Append($"{(n != 0 ? "," : "")}{'A' + n}");
                        }

                        break;

                    case PlayerCountRequirement.SOLO:
                        switch (cnt)
                        {
                            case 0: displayText.Append(" "); break;
                            case 1: displayText.Append("A"); break;
                            case 2: displayText.Append("A,<color=red>B</color>"); break;
                            case 3: displayText.Append("A,<color=red>B</color>,<color=red>C</color>"); break;
                            case 4: displayText.Append("A,<color=red>B</color>,<color=red>C</color>,<color=red>D</color>"); break;
                        }

                        break;

                    case PlayerCountRequirement.DUO:
                        switch (cnt) 
                        {
                            case 0: displayText.Append(","); break;
                            case 1: displayText.Append("A,"); break;
                            case 2: displayText.Append("A,B"); break;
                            case 3: displayText.Append("A,B,<color=red>C</color>"); break;
                            case 4: displayText.Append("A,B,<color=red>C</color>,<color=red>D</color>"); break;
                        }

                        break;
                }
                displayText.Append("]");

                if (i != children.Count - 1)
                {
                    displayText.Append(" | ");
                }
            }

            displayText.Append("</color=white>");
            parentHud.m_msgCharBuffer.Add(displayText.ToString());
            GuiManager.InteractionLayer.SetMessage(parentHud.m_msgCharBuffer, parentHud.m_msgStyle, 0);
        }

        void OnDestroy()
        {
            parent = null;
            parentHud = null;
            def = null;
            children.Clear();
            children = null;
            m_Setup = false;
            playerInScanCount = null;
            displayText = null;
        }

        static ConcurrentClusterHud()
        {
            ClassInjector.RegisterTypeInIl2Cpp<ConcurrentClusterHud>();
        }
    }
}
