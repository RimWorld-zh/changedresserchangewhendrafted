/*
 * MIT License
 * 
 * Copyright (c) [2017] [Travis Offtermatt]
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using RimWorld;
using Verse;
using ChangeDresser.UI.DTO.StorageDTOs;
using HugsLib.Source.Detour;
using System.Reflection;
using Verse.AI;
using Verse.AI.Group;

namespace ChangeDresserDraftSwitch
{
    public class DraftSwitch_Pawn_DraftController : Pawn_DraftController
    {
        static DraftSwitch_Pawn_DraftController()
        {
            BattleApparelGroupDTO.ShowForceBattleSwitch = true;
            Log.Message("ChangeDresser-Change When Drafted: detouring Pawn_DraftController.Drafted { set }");
        }

        public DraftSwitch_Pawn_DraftController(Pawn pawn) : base(pawn)
        {
            // Empty
        }

        private static FieldInfo draftedIntField = typeof(Pawn_DraftController).GetField("draftedInt", Helpers.AllBindingFlags);
        private static FieldInfo allowFiringIntField = typeof(Pawn_DraftController).GetField("allowFiringInt", Helpers.AllBindingFlags);
        private static MethodInfo notifyMasterDraftedMethod = typeof(Pawn_JobTracker).GetMethod("Notify_MasterDrafted", Helpers.AllBindingFlags);

        [DetourProperty(typeof(Pawn_DraftController), "Drafted", DetourProperty.Setter)]
        public bool _Drafted
        {
            set
            {
                if (value == (bool)draftedIntField.GetValue(this))
                {
                    return;
                }
                this.pawn.mindState.priorityWork.Clear();
                allowFiringIntField.SetValue(this, true);
                draftedIntField.SetValue(this, value);
                if (!value && this.pawn.Spawned)
                {
                    this.pawn.Map.pawnDestinationManager.UnreserveAllFor(this.pawn);
                }
                if (this.pawn.jobs.curJob != null && this.pawn.jobs.CanTakeOrderedJob())
                {
                    this.pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true);
                }
                if ((bool)draftedIntField.GetValue(this))
                {
                    foreach (Pawn current in PawnUtility.SpawnedMasteredPawns(this.pawn))
                    {
                        notifyMasterDraftedMethod.Invoke(current.jobs, null);
                    }
                    Lord lord = this.pawn.GetLord();
                    if (lord != null && lord.LordJob is LordJob_VoluntarilyJoinable)
                    {
                        lord.Notify_PawnLost(this.pawn, PawnLostCondition.Drafted);
                    }
                }
                else if (this.pawn.playerSettings != null)
                {
                    this.pawn.playerSettings.animalsReleased = false;
                }

                StorageGroupDTO storageGroupDto;
                if (BattleApparelGroupDTO.TryGetBattleApparelGroupForPawn(base.pawn, out storageGroupDto))
                {
                    storageGroupDto.SwapWith(pawn);
                }
            }
        }
    }
}
