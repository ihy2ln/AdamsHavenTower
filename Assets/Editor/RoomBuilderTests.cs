using System.Linq;
using AdamsHaven.Tower;
using NUnit.Framework;
using UnityEngine;

// Room builder slice R1 (GDD 22, owner 2026-10-05): furnished rooms where the furniture makes the rank. The builder is off
// in EditMode (and in Play until the R3 furnish UI) unless a test opts in, so the other suites keep the upgrade rules.
public sealed class RoomBuilderTests
{
    [SetUp] public void Builder() { TowerRules.InstantConstruction = true; TowerRules.ForceRoomBuilder = true; }
    [TearDown] public void Reset() { TowerRules.InstantConstruction = false; TowerRules.ForceRoomBuilder = false; }

    private static TowerRules Lot()
    {
        var rules = TowerRules.New();
        Assert.IsNull(rules.AwakenHeart());
        Assert.IsNull(rules.PlaceIntroGate());
        Assert.IsNull(rules.Build("house", 0, 21));
        Assert.IsNull(rules.ChooseStarter("kaela"));
        rules.State.eventCooldown = 100000;
        rules.State.gold = 99999; rules.State.wood = rules.State.stone = 999; rules.State.celestium = 999;
        var ground = rules.Floor(0);
        ground.west = Mathf.Max(ground.west, 6);
        ground.east = Mathf.Max(ground.east, 5);
        rules.PlaceGates();
        return rules;
    }

    private static TowerRoom Kitchen(TowerRules rules)
    {
        Assert.IsNull(rules.Build("kitchen", 0, 20));
        return rules.RoomAt(0, 20);
    }

    private static int IndexAt(TowerRoom room, int row, int col)
    { return room.furniture.FindIndex(p => p.row == row && p.col == col); }

    [Test]
    public void PresetsKeepTodaysRankPlacesStorageAndOutput()
    {
        foreach (var def in TowerCatalog.All)
        {
            if (!TowerRules.FurnishableType(def.id)) continue;
            for (int rank = 1; rank <= TowerTiers.MaxRank; rank++)
            {
                TowerRules.ForceRoomBuilder = false;
                var old = TowerRules.New();
                var before = old.AddRoom(def.id, 1, 21, rank);
                int oldPlaces = old.Capacity(before);
                float oldCap = old.StockCap(), oldYield = old.CollectAmount(before);
                int oldHousing = old.HousingCap();

                TowerRules.ForceRoomBuilder = true;
                var rules = TowerRules.New();
                var room = rules.AddRoom(def.id, 1, 21, rank);
                string what = def.id + " rank " + TowerTiers.Tier(rank) + " (" + room.width + " bays)";
                Assert.IsTrue(room.furnished, what);
                Assert.AreEqual(rank, room.level, what);
                Assert.AreEqual(rank, TowerFurnishing.CoverageRank(room), what + ": the preset earns its rank");
                Assert.GreaterOrEqual(rules.Capacity(room), oldPlaces, what + ": places");
                Assert.LessOrEqual(rules.Capacity(room), oldPlaces + 5, what + ": places");
                Assert.GreaterOrEqual(rules.HousingCap(), oldHousing, what + ": housing");
                Assert.AreEqual(oldCap, rules.StockCap(), 0.01f, what + ": stock cap");
                Assert.AreEqual(oldYield, rules.CollectAmount(room), 0.01f, what + ": collect");
                Assert.AreEqual(def.produces ?? "", rules.Product(room), what + ": product");
                foreach (var piece in room.furniture)
                {
                    Assert.IsNull(rules.CanPlaceFurniture(room, piece.item, piece.tier, piece.col, piece.row, piece), what + ": " + piece.item);
                    Assert.Less(piece.col + TowerFurnishing.Get(piece.item).cols - 1, room.width * TowerFurnishing.ColsPerBay, what);
                }
            }
        }
    }

    [Test]
    public void AnOlderSaveIsFurnishedOnceOnLoadWithoutLosingRanks()
    {
        TowerRules.ForceRoomBuilder = false;
        var rules = Lot();
        var kitchen = Kitchen(rules);
        Assert.IsNull(rules.UpgradeRoom(kitchen.uid));
        Assert.AreEqual(2, kitchen.level);
        Assert.IsFalse(kitchen.furnished);
        string json = JsonUtility.ToJson(rules.State);

        TowerRules.ForceRoomBuilder = true;
        var loaded = new TowerRules(JsonUtility.FromJson<TowerState>(json));
        var room = loaded.RoomAt(0, 20);
        Assert.IsTrue(room.furnished);
        Assert.AreEqual(2, room.level);
        Assert.Greater(room.furniture.Count, 0);
        Assert.IsTrue(loaded.State.rooms.Where(r => TowerRules.FurnishableType(r.type)).All(r => r.furnished));

        // A player who sells everything is not re-furnished on the next load.
        while (room.furniture.Count > 0) Assert.IsNull(loaded.SellFurniture(room.uid, 0));
        var again = new TowerRules(JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(loaded.State)));
        Assert.AreEqual(0, again.RoomAt(0, 20).furniture.Count);
        Assert.AreEqual(1, again.RoomAt(0, 20).level);
    }

    [Test]
    public void FurnitureRaisesAndLowersTheRankWithinTheHeartsCap()
    {
        var rules = Lot();
        var kitchen = Kitchen(rules);
        Assert.AreEqual(1, kitchen.level);
        Assert.AreEqual("Furnish the room to raise its rank.", rules.UpgradeRoom(kitchen.uid));

        // The F preset: two stoves at the back. Two D stoves and a D table make three weighted columns at D.
        Assert.IsNull(rules.PlaceFurniture(kitchen.uid, "stove", 3, 2, TowerFurnishing.Back));
        Assert.IsNull(rules.PlaceFurniture(kitchen.uid, "stove", 3, 3, TowerFurnishing.Back));
        Assert.AreEqual(1, kitchen.level);
        Assert.IsNull(rules.PlaceFurniture(kitchen.uid, "table", 3, 0, TowerFurnishing.Front));
        Assert.AreEqual(3, kitchen.level);

        // A rank F Heart caps buildings at D: C furniture is not for sale yet.
        StringAssert.Contains("Heart", rules.PlaceFurniture(kitchen.uid, "rug", 4, 1, TowerFurnishing.Front));

        // Rank follows the furniture back down.
        Assert.IsNull(rules.SellFurniture(kitchen.uid, IndexAt(kitchen, TowerFurnishing.Front, 0)));
        Assert.AreEqual(1, kitchen.level);
    }

    [Test]
    public void SlotsFollowRowsWidthAndNeighbours()
    {
        var rules = Lot();
        var kitchen = Kitchen(rules);
        StringAssert.Contains("back row", rules.CanPlaceFurniture(kitchen, "stove", 1, 2, TowerFurnishing.Front));
        StringAssert.Contains("already there", rules.CanPlaceFurniture(kitchen, "stove", 1, 0, TowerFurnishing.Back));
        StringAssert.Contains("does not fit", rules.CanPlaceFurniture(kitchen, "saw_bench", 1, 3, TowerFurnishing.Back));
        Assert.IsNull(rules.CanPlaceFurniture(kitchen, "saw_bench", 1, 2, TowerFurnishing.Back));
        Assert.IsNull(rules.PlaceFurniture(kitchen.uid, "crate", 1, 1, TowerFurnishing.Front));
        int crate = IndexAt(kitchen, TowerFurnishing.Front, 1);
        Assert.IsNull(rules.MoveFurniture(kitchen.uid, crate, 3));
        Assert.AreEqual(3, kitchen.furniture[crate].col);
        int gold = rules.State.gold;
        Assert.IsNull(rules.SellFurniture(kitchen.uid, crate));
        Assert.AreEqual(gold + rules.FurnitureRefund(new TowerFurniture { item = "crate", tier = 1 }), rules.State.gold);
    }

    [Test]
    public void AnyItemWorksInAnyRoomAndMatchingItemsDoBetter()
    {
        var rules = Lot();
        var shack = rules.RoomAt(0, 21);
        int housing = rules.HousingCap();
        Assert.AreEqual("", rules.Product(shack));
        Assert.IsTrue(rules.HousesByDefault(shack));

        // A stove in the Shack cooks at 80%, and the Shack becomes a workplace whose beds still count.
        Assert.IsNull(rules.PlaceFurniture(shack.uid, "stove", 1, 2, TowerFurnishing.Back));
        Assert.AreEqual("food", rules.Product(shack));
        Assert.IsFalse(rules.HousesByDefault(shack));
        Assert.AreEqual(1, rules.Capacity(shack));
        Assert.AreEqual(housing, rules.HousingCap());
        Assert.AreEqual(4f * 0.8f, rules.YieldOf(shack, "food"), 0.01f);

        // Beds in a Kitchen add housing; Assign still makes it a job, AssignHome a bed.
        var kitchen = Kitchen(rules);
        Assert.IsNull(rules.PlaceFurniture(kitchen.uid, "bed", 1, 2, TowerFurnishing.Back));
        Assert.AreEqual(housing + 1, rules.HousingCap());
        var cook = rules.State.residents[0];
        Assert.IsNull(rules.Assign(cook.id, kitchen.uid));
        Assert.AreEqual(kitchen.uid, cook.jobRoom);
        Assert.IsNull(rules.AssignHome(cook.id, kitchen.uid));
        Assert.AreEqual(kitchen.uid, cook.homeRoom);

        // Off-type storage holds 80%.
        float cap = rules.StockCap();
        Assert.IsNull(rules.PlaceFurniture(kitchen.uid, "stock_rack", 1, 3, TowerFurnishing.Back));
        Assert.AreEqual(cap + 0.8f * 2 * TowerFurnishing.StoragePerUnit, rules.StockCap(), 0.01f);
    }

    [Test]
    public void AMixedRoomCollectsEverythingItMakes()
    {
        var rules = Lot();
        var kitchen = Kitchen(rules);
        Assert.IsNull(rules.PlaceFurniture(kitchen.uid, "pump", 1, 2, TowerFurnishing.Back));
        Assert.AreEqual("food", rules.Product(kitchen));
        Assert.IsTrue(rules.Makes(kitchen, "water"));
        float food = rules.State.food, water = rules.State.water;
        float foodYield = rules.YieldOf(kitchen, "food"), waterYield = rules.YieldOf(kitchen, "water");
        Assert.Greater(waterYield, 0);
        kitchen.ready = true;
        Assert.IsNull(rules.Collect(kitchen.uid));
        Assert.AreEqual(Mathf.Min(rules.StockCap(), food + foodYield), rules.State.food, 0.01f);
        Assert.AreEqual(Mathf.Min(rules.StockCap(), water + waterYield), rules.State.water, 0.01f);
    }

    [Test]
    public void RoomsExpandAndShrinkByTheBay()
    {
        var rules = Lot();
        var kitchen = Kitchen(rules);
        int stoves = kitchen.furniture.Count(p => p.item == "stove");
        Assert.IsNull(rules.ExpandRoom(kitchen.uid));
        Assert.AreEqual(2, kitchen.width);
        Assert.AreEqual(19, kitchen.x);
        // The room grew to the left: everything inside slid one bay right, the new bay is empty.
        Assert.IsTrue(kitchen.furniture.All(p => p.col >= TowerFurnishing.ColsPerBay));
        Assert.AreEqual(stoves, kitchen.furniture.Count(p => p.item == "stove"));
        Assert.AreEqual(1, kitchen.level);   // half-furnished at twice the width

        Assert.IsNull(rules.PlaceFurniture(kitchen.uid, "rug", 1, 0, TowerFurnishing.Front));
        StringAssert.Contains("Clear the left bay", rules.ShrinkRoom(kitchen.uid));
        Assert.IsNull(rules.SellFurniture(kitchen.uid, IndexAt(kitchen, TowerFurnishing.Front, 0)));
        Assert.IsNull(rules.ShrinkRoom(kitchen.uid));
        Assert.AreEqual(1, kitchen.width);
        Assert.AreEqual(20, kitchen.x);
        Assert.AreEqual(stoves, kitchen.furniture.Count(p => p.item == "stove" && p.col < TowerFurnishing.ColsPerBay));
        StringAssert.Contains("at least one bay", rules.ShrinkRoom(kitchen.uid));

        // The Shack is no longer stuck at one bay, but a room tops out at three.
        var shack = rules.RoomAt(0, 21);
        Assert.IsNull(rules.ExpandRoom(kitchen.uid));
        Assert.IsNull(rules.ExpandRoom(kitchen.uid));
        StringAssert.Contains("at most", rules.ExpandRoom(kitchen.uid));
        StringAssert.Contains("free and founded", rules.ExpandRoom(shack.uid));
    }

    [Test]
    public void SellingBedsMovesTheLastSleepersOut()
    {
        var rules = Lot();
        var shack = rules.RoomAt(0, 21);
        var sleeper = rules.State.residents.First(r => r.homeRoom == shack.uid);
        while (shack.furniture.Any(p => p.item == "bed"))
            Assert.IsNull(rules.SellFurniture(shack.uid, shack.furniture.FindIndex(p => p.item == "bed")));
        Assert.AreEqual(0, rules.HomePlaces(shack));
        Assert.AreNotEqual(shack.uid, sleeper.homeRoom);
    }

    [Test]
    public void FurnitureSurvivesASave()
    {
        var rules = Lot();
        var kitchen = Kitchen(rules);
        Assert.IsNull(rules.PlaceFurniture(kitchen.uid, "plant", 2, 3, TowerFurnishing.Front));
        int count = kitchen.furniture.Count;
        var loaded = new TowerRules(JsonUtility.FromJson<TowerState>(JsonUtility.ToJson(rules.State)));
        var room = loaded.RoomAt(0, 20);
        Assert.AreEqual(count, room.furniture.Count);
        Assert.IsTrue(room.furniture.Any(p => p.item == "plant" && p.tier == 2 && p.col == 3 && p.row == TowerFurnishing.Front));
    }
}
