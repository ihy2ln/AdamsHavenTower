using System;
using System.Collections.Generic;
using UnityEngine;

namespace AdamsHaven.Tower
{
    // What a finished build or upgrade cost, so it can be sold back for a while (TT 10.4.1).
    [Serializable] public sealed class TowerReceipt
    {
        public int room;
        public string kind = "build";      // build or upgrade
        public int gold, wood, stone;
        public float at;                   // game clock when it finished
        public int fromLevel, fromX, fromWidth;   // upgrade: the room as it was before
    }

    // Owner ruling (2026-10-04): a building or upgrade sells back at 100% the moment it finishes, falling to 0% over
    // SellWindow game seconds; an upgrade sold back undoes itself. After the window a building must be deconstructed
    // (timed, nothing back). Construction still under way can be cancelled for everything it cost.
    public sealed partial class TowerRules
    {
        public const float SellWindow = 180f;

        private TowerReceipt LastReceipt(int roomUid) { return State.receipts.FindLast(r => r.room == roomUid); }

        private void RecordReceipt(TowerRoom room, string kind, int gold, int wood, int stone, int fromLevel = 0, int fromX = 0, int fromWidth = 0)
        {
            if (room == null) return;
            State.receipts.Add(new TowerReceipt { room = room.uid, kind = kind, gold = gold, wood = wood, stone = stone, at = State.clock,
                fromLevel = fromLevel, fromX = fromX, fromWidth = fromWidth });
        }

        private void PruneReceipts()
        {
            if (State.receipts.Count > 0) State.receipts.RemoveAll(r => State.clock - r.at > SellWindow || Room(r.room) == null);
        }

        // 1 the moment it finished, 0 once the window has closed.
        public float SellShare(TowerRoom room)
        {
            var receipt = room == null ? null : LastReceipt(room.uid);
            return receipt == null ? 0 : Mathf.Clamp01(1 - (State.clock - receipt.at) / SellWindow);
        }

        public float SellSecondsLeft(TowerRoom room)
        {
            var receipt = room == null ? null : LastReceipt(room.uid);
            return receipt == null ? 0 : Mathf.Max(0, SellWindow - (State.clock - receipt.at));
        }

        public bool SellingUndoesUpgrade(TowerRoom room)
        {
            var receipt = room == null ? null : LastReceipt(room.uid);
            return receipt != null && receipt.kind == "upgrade" && SellShare(room) > 0;
        }

        // Gold back if the room were sold now (0 past the window).
        public int DemolishRefund(TowerRoom room)
        {
            var receipt = room == null ? null : LastReceipt(room.uid);
            return receipt == null ? 0 : Mathf.FloorToInt(receipt.gold * SellShare(room));
        }

        public TowerWork DeconstructWork(int roomUid) { return State.works.Find(w => w.kind == "deconstruct" && w.room == roomUid); }

        public float DeconstructSeconds(TowerRoom room) { return room == null ? 0 : RoomBuildSeconds(room.type) * 0.5f; }

        // The room card's one button: sell back inside the window, otherwise deconstruct.
        public string Demolish(int roomUid)
        {
            var room = Room(roomUid);
            if (room == null || room.type == "heart" || room.type == "gate") return "The Heart and Gate stay.";
            if (State.incidents.Exists(i => i.roomUid == roomUid)) return "Resolve the incident first.";
            if (State.introPhase != "complete") return "Finish founding the Tower first.";
            if (DeconstructWork(roomUid) != null) return "It is already being taken down.";
            return SellShare(room) > 0 ? SellBack(room) : Deconstruct(room);
        }

        private string SellBack(TowerRoom room)
        {
            var receipt = LastReceipt(room.uid);
            float share = SellShare(room);
            int gold = Mathf.FloorToInt(receipt.gold * share), wood = Mathf.FloorToInt(receipt.wood * share),
                stone = Mathf.FloorToInt(receipt.stone * share);
            State.receipts.Remove(receipt);
            State.gold += gold; State.wood += wood; State.stone += stone;
            string name = TowerCatalog.Get(room.type).displayName;
            string back = gold + " gold" + (wood + stone > 0 ? ", " + wood + " wood and " + stone + " stone" : "") +
                " (" + Mathf.RoundToInt(share * 100) + "%)";
            if (receipt.kind == "upgrade")
            {
                room.level = receipt.fromLevel; room.x = receipt.fromX; room.width = receipt.fromWidth;
                TouchLayout();
                Note("Undid the " + name + " upgrade and got back " + back + ".");
                Emit("upgrade", room.uid, 0, room.level.ToString());
                return null;
            }
            RemoveRoom(room);
            Note("Sold back the " + name + " for " + back + ".");
            Emit("demolish", 0, 0, name);
            return null;
        }

        private string Deconstruct(TowerRoom room)
        {
            string name = TowerCatalog.Get(room.type).displayName;
            if (!Timed)
            {
                RemoveRoom(room);
                Note("Took down the " + name + ".");
                Emit("demolish", 0, 0, name);
                return null;
            }
            float seconds = DeconstructSeconds(room);
            StartWork("deconstruct", room.floor, room.x, 0, room.type, seconds);
            State.works[State.works.Count - 1].room = room.uid;
            Note("Started taking down the " + name + " (" + Clock(seconds) + "). Nothing comes back once the sell-back window has closed.");
            return null;
        }

        private void RemoveRoom(TowerRoom room)
        {
            State.rooms.Remove(room); roomIndex = null;
            State.receipts.RemoveAll(r => r.room == room.uid);
            State.works.RemoveAll(w => w.kind == "deconstruct" && w.room == room.uid);
            Evacuate(room.uid);
            TouchLayout();
        }

        // ---- Cancelling construction --------------------------------------------------------------------------

        // Everything the work cost comes back; a deconstruction simply stops.
        public string CancelWork(TowerWork work)
        {
            if (work == null || !State.works.Contains(work)) return "Nothing is being built there.";
            State.works.Remove(work);
            if (work.kind == "deconstruct")
            {
                var room = Room(work.room);
                Note("Stopped taking down the " + (room == null ? "room" : TowerCatalog.Get(room.type).displayName) + ".");
                return null;
            }
            State.gold += work.gold; State.wood += work.wood; State.stone += work.stone; State.celestium += work.celestium;
            if (work.kind == "wing" && work.floor == 0) PlaceGates();   // the Gate steps back in
            string what = work.kind == "room" ? TowerCatalog.Get(work.type).displayName : work.kind == "wing" ?
                (work.side < 0 ? "west" : "east") + " foundation of floor " + work.floor : "floor " + work.floor;
            Note("Cancelled the " + what + ". Refunded " + RefundText(work.gold, work.wood, work.stone, work.celestium) + ".");
            Emit("demolish", 0, 0, what);
            return null;
        }

        public static string RefundText(int gold, int wood, int stone, int celestium)
        {
            var parts = new List<string>();
            if (gold > 0) parts.Add(gold + " gold");
            if (wood > 0) parts.Add(wood + " wood");
            if (stone > 0) parts.Add(stone + " stone");
            if (celestium > 0) parts.Add(celestium + " Celestium");
            return parts.Count == 0 ? "nothing" : string.Join(", ", parts.ToArray());
        }

        // Stamps what a work just cost onto it, so cancelling can return it.
        private void PriceLastWork(int gold, int wood, int stone, int celestium)
        {
            if (State.works.Count == 0) return;
            var work = State.works[State.works.Count - 1];
            work.gold = gold; work.wood = wood; work.stone = stone; work.celestium = celestium;
        }
    }
}
