using ChainedPuzzles;
using Il2CppInterop.Runtime.Injection;
using Player;
using ScanPosOverride.PuzzleOverrideData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ScanPosOverride.Component
{
    internal class ConcurrentClusterHud: MonoBehaviour
    {
        internal CP_Cluster_Core parent { get; set; }

        internal CP_Bioscan_Hud parentHud { get; set; }

        internal PuzzleOverride def { get; set; }

        //private string text = string.Empty;

        private StringBuilder displayText = new();

        private bool m_isValid = false;
        
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
                            m_isValid = true;     
                            break;
                        
                        case PlayerCountRequirement.DUO:     
                            m_isValid = true;     
                            break;
                    }

                    break;

                case 4:
                    switch (playerCountReq)
                    {
                        case PlayerCountRequirement.ANY:
                        case PlayerCountRequirement.SOLO: 
                            m_isValid = true;     
                            break;
                    }

                    break;

                case 3:
                    switch (playerCountReq)
                    {
                        case PlayerCountRequirement.ANY:
                        case PlayerCountRequirement.SOLO: 
                            m_isValid = true;
                            break;
                    }

                    break;
            }

            if (!m_isValid)
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
            if (!m_isValid || !parentHud.m_visible || !parentHud.m_isClosestToPlayer) return;

            displayText.Clear();
            displayText.AppendLine().Append("<color=white>");

            for (int i = 0; i < children.Count; i++)
            {
                int cnt = 0;
                var child = children[i];
                foreach(var p in PlayerManager.PlayerAgentsInLevel)
                {
                    if ((p.Position - child.transform.position).magnitude < childRadius)
                    {
                        cnt += 1;
                    }
                }

                playerInScanCount[i] = cnt;

                const string ORANGE = "orange";
                const string WHITE = "white";

                string childText = string.Empty;
                string childColor = string.Empty;
                switch(playerCountReq)
                {
                    case PlayerCountRequirement.ANY:
                        switch (cnt)
                        {
                            case 0: childText = " "; break;
                            case 1: childText = "A"; break;
                            case 2: childText = "A,B"; break;
                            case 3: childText = "A,B,C"; break;
                            case 4: childText = "A,B,C,D"; break;
                        }

                        childColor = cnt > 0 ? WHITE : ORANGE;                        
                        break;

                    case PlayerCountRequirement.SOLO:
                        switch (cnt)
                        {
                            case 0: 
                                childText = " "; 
                                childColor = ORANGE; 
                                break;
                            
                            case 1: 
                                childText = "A"; 
                                childColor = WHITE;  
                                break;
                            
                            case 2: 
                                childText = "A,<color=red>B</color>"; 
                                childColor = ORANGE; 
                                break;
                            
                            case 3: 
                                childText = "A,<color=red>B</color>,<color=red>C</color>"; 
                                childColor = ORANGE; 
                                break;
                            
                            case 4: 
                                childText = "A,<color=red>B</color>,<color=red>C</color>,<color=red>D</color>"; 
                                childColor = ORANGE; 
                                break;
                        }

                        break;

                    case PlayerCountRequirement.DUO:
                        switch (cnt) 
                        {
                            case 0: 
                                childText = " , "; 
                                childColor = ORANGE;
                                break;
                            
                            case 1: childText = "A, "; 
                                childColor = ORANGE;
                                break;
                            
                            case 2: 
                                childText = "A,B"; 
                                childColor = WHITE;
                                break;

                            case 3: 
                                childText = "A,B,<color=red>C</color>"; 
                                childColor = ORANGE;
                                break;
                            case 4: 
                                childText = "A,B,<color=red>C</color>,<color=red>D</color>"; 
                                childColor = ORANGE;
                                break;
                        }

                        break;
                }
                displayText.Append($"<color={childColor}>")
                    .Append(ROMAN[i]).Append(": ")
                    .Append("[")
                    .Append(childText)
                    .Append("]")
                    .Append($"</color={childColor}>");

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
            m_isValid = false;
            playerInScanCount = null;
            displayText = null;
        }

        static ConcurrentClusterHud()
        {
            ClassInjector.RegisterTypeInIl2Cpp<ConcurrentClusterHud>();
        }
    }
}
