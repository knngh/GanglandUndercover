using System.Collections.Generic;
using UnityEngine;

namespace GanglandUndercover.Art
{
    /// <summary>
    /// 完整 2D 像素美术资产缓存（64×64 角色 / 32×32 tile）。
    /// 100% 程序化生成——无外部依赖。所有 sprite 在此集中管理。
    /// </summary>
    public static class Sprite2DAssetCache
    {
        // ══════════════════════════════════════════════════════
        // 角色（每职业×4向×3帧+死亡帧 = 每职业13个sprite）
        // ══════════════════════════════════════════════════════
        public static readonly Dictionary<Online.OnlineProfession, ProfSpriteSet> CharacterSets = new();

        // 默认角色（fallback）
        public static Sprite CharBody_Front, CharBody_Back, CharBody_Left, CharBody_Right;
        public static Sprite CorpseMarker;
        public static Sprite CharDirectionArrow;
        public static Sprite CharGhostOverlay;

        // ══════════════════════════════════════════════════════
        // 地图 Tile（32×32）
        // ══════════════════════════════════════════════════════
        public static Sprite FloorWood, FloorConcrete, FloorMetal, FloorCarpet, FloorTile;
        public static Sprite WallBrick, WallConcrete, WallStripe;
        public static Sprite FloorTileAlt, CorridorTile;
        public static Sprite WallBlock;

        // ══════════════════════════════════════════════════════
        // 道具（32×32）
        // ══════════════════════════════════════════════════════
        public static Sprite PropCrate, PropBarrel, PropKowloonCrate, PropDesk, PropCabinet, PropEvidenceBox;
        public static Sprite LandmarkOfficeSign1, LandmarkOfficeSign2, LandmarkMailbox, LandmarkTruckFront;
        public static Sprite LandmarkUmbrella, LandmarkTinyTable, LandmarkPackage, LandmarkAirDuct;
        public static Sprite LandmarkDoorOpen, LandmarkPottedPlant;
        public static Sprite RoomPropGenerator, RoomPropMonitor, RoomPropControlBigMonitor, RoomPropToolBox;
        public static Sprite RoomPropLightTower, RoomPropSosBox, RoomPropChair, RoomPropBenchedTable;
        public static Sprite RoomPropTrashCan, RoomPropTrashPile;
        public static Sprite OfficeRoomPropWhiteboard, OfficeRoomPropChartBoard, OfficeRoomPropServerRack;
        public static Sprite OfficeRoomPropPrinter, OfficeRoomPropCornerDesk, OfficeRoomPropDualMonitorDesk;
        public static Sprite OfficeRoomPropCctvCameraRig, OfficeRoomPropMedicalCart;
        public static Sprite InteriorRoomPropHospitalResonanceMachine, InteriorRoomPropHospitalScreenColor;
        public static Sprite InteriorRoomPropHospitalSink, InteriorRoomPropHospitalXrayMachine;
        public static Sprite InteriorRoomPropHospitalXrayScreen, InteriorRoomPropMorgueFreezerCorpseDoor;
        public static Sprite InteriorRoomPropSecurityCameraWallRight, InteriorRoomPropSafeGold;
        public static Sprite InteriorRoomPropSafeBucks, InteriorRoomPropGroceryCheckoutRoller;
        public static Sprite InteriorRoomPropGroceryGlassFridge, InteriorRoomPropButcherCarcass;
        public static Sprite InteriorRoomPropKitchenBbq, InteriorRoomPropKitchenOven4Cookers;
        public static Sprite InteriorRoomPropKitchenSink, InteriorRoomPropCanteenCakeFridge;
        public static Sprite InteriorRoomPropShoppingCartBlueFull, InteriorRoomPropOldTv;
        public static Sprite InteriorRoomPropJailLockerFull, InteriorRoomPropMuseumLaserHorizontal;
        public static Sprite InteriorRoomPropTrapdoor, InteriorRoomPropTicketMachine;
        public static Sprite InteriorRoomPropHospitalTvReportage, InteriorRoomPropFishCuttingSink;
        public static Sprite VentIcon, KowloonVentIcon, CameraIcon;

        public const string HarbourPropCrateWoodPath = "Sprites/Tilesets/Harbour/props/tile_crate_wood";
        public const string HarbourPropBarrelOilPath = "Sprites/Tilesets/Harbour/props/tile_barrel_oil";
        public const string HarbourPropVentBackalleyPath = "Sprites/Tilesets/Harbour/props/tile_vent_backalley";
        public const string KowloonPropCrateOldPath = "Sprites/Tilesets/KowloonWalledCity/props/tile_crate_old";
        public const string KowloonPropVentRustPath = "Sprites/Tilesets/KowloonWalledCity/props/tile_vent_rust";
        public const string LimeZuInteriorFloorTilePath = "Sprites/Tilesets/LimeZu/Interiors/floors/room-builder-floors-16";
        public const string LimeZuExteriorFloorTilePath = "Sprites/Tilesets/LimeZu/Exteriors/floors/asphalt-48-a";
        public const string LimeZuInteriorWallTilePath = "Sprites/Tilesets/LimeZu/Interiors/walls/room-builder-walls-16";
        public const string LimeZuOfficePropsTilePath = "Sprites/Tilesets/LimeZu/Office/props/modern-office-16";
        public const string LimeZuOfficeWallPropsTilePath = "Sprites/Tilesets/LimeZu/Office/walls/office-walls-floors-16";
        public const string LimeZuOfficeFloorPropsTilePath = "Sprites/Tilesets/LimeZu/Office/floors/room-builder-office-16";
        public const string LimeZuLandmarkOfficeSign1Path = "Sprites/Tilesets/LimeZu/Exteriors/landmarks/ME_Singles_Office_48x48_Building_Sign_1";
        public const string LimeZuLandmarkOfficeSign2Path = "Sprites/Tilesets/LimeZu/Exteriors/landmarks/ME_Singles_Office_48x48_Building_Sign_2";
        public const string LimeZuLandmarkMailboxPath = "Sprites/Tilesets/LimeZu/Exteriors/landmarks/22_Post_Office_48x48_Blue_Mailbox_1_Front";
        public const string LimeZuLandmarkTruckFrontPath = "Sprites/Tilesets/LimeZu/Exteriors/landmarks/22_Post_Office_48x48_Truck_Front";
        public const string LimeZuLandmarkUmbrellaPath = "Sprites/Tilesets/LimeZu/Exteriors/landmarks/24_Additional_Houses_Modern_House_Umbrella_Right_48x48";
        public const string LimeZuLandmarkTinyTablePath = "Sprites/Tilesets/LimeZu/Exteriors/landmarks/24_Additional_Houses_Modern_House_Tiny_Table_With_Drinks_1_48x48";
        public const string LimeZuLandmarkPackagePath = "Sprites/Tilesets/LimeZu/Exteriors/landmarks/22_Post_Office_48x48_Big_Single_Package_1";
        public const string LimeZuLandmarkAirDuctPath = "Sprites/Tilesets/LimeZu/Exteriors/landmarks/ME_Singles_Office_48x48_Air_Duct_3_Roof_Prop";
        public const string LimeZuLandmarkDoorOpenPath = "Sprites/Tilesets/LimeZu/Exteriors/landmarks/24_Additional_Houses_Modern_House_Door_Open_48x48";
        public const string LimeZuLandmarkPottedPlantPath = "Sprites/Tilesets/LimeZu/Exteriors/landmarks/24_Additional_Houses_Modern_House_Potted_Plant_48x48";
        public const string LimeZuRoomPropGeneratorPath = "Sprites/Tilesets/LimeZu/Exteriors/room-props/24_Additional_Houses_Post_Apocalyptic_House_Generator_2_48x48";
        public const string LimeZuRoomPropMonitorPath = "Sprites/Tilesets/LimeZu/Exteriors/room-props/ME_Singles_Subway_and_Train_Station_48x48_Monitor";
        public const string LimeZuRoomPropControlBigMonitorPath = "Sprites/Tilesets/LimeZu/Exteriors/room-props/ME_Singles_Subway_and_Train_Station_48x48_Control_Big_Monitor";
        public const string LimeZuRoomPropToolBoxPath = "Sprites/Tilesets/LimeZu/Exteriors/room-props/ME_Singles_Worksite_48x48_Tool_Box_1";
        public const string LimeZuRoomPropLightTowerPath = "Sprites/Tilesets/LimeZu/Exteriors/room-props/ME_Singles_Worksite_48x48_Light_Tower_1";
        public const string LimeZuRoomPropSosBoxPath = "Sprites/Tilesets/LimeZu/Exteriors/room-props/ME_Singles_Subway_and_Train_Station_48x48_SOS_Box";
        public const string LimeZuRoomPropChairPath = "Sprites/Tilesets/LimeZu/Exteriors/room-props/ME_Singles_Camping_48x48_Chair_1";
        public const string LimeZuRoomPropBenchedTablePath = "Sprites/Tilesets/LimeZu/Exteriors/room-props/ME_Singles_Camping_48x48_Benched_Table_2";
        public const string LimeZuRoomPropTrashCanPath = "Sprites/Tilesets/LimeZu/Exteriors/room-props/ME_Singles_City_Props_48x48_Black_Closed_Trash_Can";
        public const string LimeZuRoomPropTrashPilePath = "Sprites/Tilesets/LimeZu/Exteriors/room-props/ME_Singles_City_Props_48x48_Trash_Pile_1";
        public const string LimeZuOfficeRoomPropWhiteboardPath = "Sprites/Tilesets/LimeZu/Office/room-props/Modern_Office_Singles_48x48_170_Whiteboard";
        public const string LimeZuOfficeRoomPropChartBoardPath = "Sprites/Tilesets/LimeZu/Office/room-props/Modern_Office_Singles_48x48_172_ChartBoard";
        public const string LimeZuOfficeRoomPropServerRackPath = "Sprites/Tilesets/LimeZu/Office/room-props/Modern_Office_Singles_48x48_176_ServerRack";
        public const string LimeZuOfficeRoomPropPrinterPath = "Sprites/Tilesets/LimeZu/Office/room-props/Modern_Office_Singles_48x48_178_Printer";
        public const string LimeZuOfficeRoomPropCornerDeskPath = "Sprites/Tilesets/LimeZu/Office/room-props/Modern_Office_Singles_48x48_203_CornerDesk";
        public const string LimeZuOfficeRoomPropDualMonitorDeskPath = "Sprites/Tilesets/LimeZu/Office/room-props/Modern_Office_Singles_48x48_227_DualMonitorDesk";
        public const string LimeZuOfficeRoomPropCctvCameraRigPath = "Sprites/Tilesets/LimeZu/Office/room-props/Modern_Office_Singles_48x48_276_CctvCameraRig";
        public const string LimeZuOfficeRoomPropMedicalCartPath = "Sprites/Tilesets/LimeZu/Office/room-props/Modern_Office_Singles_48x48_320_MedicalCart";
        public const string LimeZuInteriorRoomPropHospitalResonanceMachinePath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_HospitalResonanceMachine";
        public const string LimeZuInteriorRoomPropHospitalScreenColorPath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_HospitalScreenColor";
        public const string LimeZuInteriorRoomPropHospitalSinkPath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_HospitalSink";
        public const string LimeZuInteriorRoomPropHospitalXrayMachinePath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_HospitalXrayMachine";
        public const string LimeZuInteriorRoomPropHospitalXrayScreenPath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_HospitalXrayScreen";
        public const string LimeZuInteriorRoomPropMorgueFreezerCorpseDoorPath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_MorgueFreezerCorpseDoor";
        public const string LimeZuInteriorRoomPropSecurityCameraWallRightPath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_SecurityCameraWallRight";
        public const string LimeZuInteriorRoomPropSafeGoldPath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_SafeGold";
        public const string LimeZuInteriorRoomPropSafeBucksPath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_SafeBucks";
        public const string LimeZuInteriorRoomPropGroceryCheckoutRollerPath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_GroceryCheckoutRoller";
        public const string LimeZuInteriorRoomPropGroceryGlassFridgePath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_GroceryGlassFridge";
        public const string LimeZuInteriorRoomPropButcherCarcassPath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_ButcherCarcass";
        public const string LimeZuInteriorRoomPropKitchenBbqPath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_KitchenBbq";
        public const string LimeZuInteriorRoomPropKitchenOven4CookersPath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_KitchenOven4Cookers";
        public const string LimeZuInteriorRoomPropKitchenSinkPath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_KitchenSink";
        public const string LimeZuInteriorRoomPropCanteenCakeFridgePath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_CanteenCakeFridge";
        public const string LimeZuInteriorRoomPropShoppingCartBlueFullPath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_ShoppingCartBlueFull";
        public const string LimeZuInteriorRoomPropOldTvPath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_OldTv";
        public const string LimeZuInteriorRoomPropJailLockerFullPath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_JailLockerFull";
        public const string LimeZuInteriorRoomPropMuseumLaserHorizontalPath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_MuseumLaserHorizontal";
        public const string LimeZuInteriorRoomPropTrapdoorPath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_Trapdoor";
        public const string LimeZuInteriorRoomPropTicketMachinePath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_TicketMachine";
        public const string LimeZuInteriorRoomPropHospitalTvReportagePath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_HospitalTvReportage";
        public const string LimeZuInteriorRoomPropFishCuttingSinkPath = "Sprites/Tilesets/LimeZu/Interiors/room-props/Modern_Interiors_48x48_FishCuttingSink";
        public static string FloorTileAltResourcePath { get; private set; } = string.Empty;
        public static string CorridorTileResourcePath { get; private set; } = string.Empty;
        public static string WallBlockResourcePath { get; private set; } = string.Empty;
        public static string PropCrateResourcePath { get; private set; } = string.Empty;
        public static string PropBarrelResourcePath { get; private set; } = string.Empty;
        public static string KowloonPropCrateResourcePath { get; private set; } = string.Empty;
        public static string PropDeskResourcePath { get; private set; } = string.Empty;
        public static string PropCabinetResourcePath { get; private set; } = string.Empty;
        public static string PropEvidenceBoxResourcePath { get; private set; } = string.Empty;
        public static string LandmarkOfficeSign1ResourcePath { get; private set; } = string.Empty;
        public static string LandmarkOfficeSign2ResourcePath { get; private set; } = string.Empty;
        public static string LandmarkMailboxResourcePath { get; private set; } = string.Empty;
        public static string LandmarkTruckFrontResourcePath { get; private set; } = string.Empty;
        public static string LandmarkUmbrellaResourcePath { get; private set; } = string.Empty;
        public static string LandmarkTinyTableResourcePath { get; private set; } = string.Empty;
        public static string LandmarkPackageResourcePath { get; private set; } = string.Empty;
        public static string LandmarkAirDuctResourcePath { get; private set; } = string.Empty;
        public static string LandmarkDoorOpenResourcePath { get; private set; } = string.Empty;
        public static string LandmarkPottedPlantResourcePath { get; private set; } = string.Empty;
        public static string RoomPropGeneratorResourcePath { get; private set; } = string.Empty;
        public static string RoomPropMonitorResourcePath { get; private set; } = string.Empty;
        public static string RoomPropControlBigMonitorResourcePath { get; private set; } = string.Empty;
        public static string RoomPropToolBoxResourcePath { get; private set; } = string.Empty;
        public static string RoomPropLightTowerResourcePath { get; private set; } = string.Empty;
        public static string RoomPropSosBoxResourcePath { get; private set; } = string.Empty;
        public static string RoomPropChairResourcePath { get; private set; } = string.Empty;
        public static string RoomPropBenchedTableResourcePath { get; private set; } = string.Empty;
        public static string RoomPropTrashCanResourcePath { get; private set; } = string.Empty;
        public static string RoomPropTrashPileResourcePath { get; private set; } = string.Empty;
        public static string OfficeRoomPropWhiteboardResourcePath { get; private set; } = string.Empty;
        public static string OfficeRoomPropChartBoardResourcePath { get; private set; } = string.Empty;
        public static string OfficeRoomPropServerRackResourcePath { get; private set; } = string.Empty;
        public static string OfficeRoomPropPrinterResourcePath { get; private set; } = string.Empty;
        public static string OfficeRoomPropCornerDeskResourcePath { get; private set; } = string.Empty;
        public static string OfficeRoomPropDualMonitorDeskResourcePath { get; private set; } = string.Empty;
        public static string OfficeRoomPropCctvCameraRigResourcePath { get; private set; } = string.Empty;
        public static string OfficeRoomPropMedicalCartResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropHospitalResonanceMachineResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropHospitalScreenColorResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropHospitalSinkResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropHospitalXrayMachineResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropHospitalXrayScreenResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropMorgueFreezerCorpseDoorResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropSecurityCameraWallRightResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropSafeGoldResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropSafeBucksResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropGroceryCheckoutRollerResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropGroceryGlassFridgeResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropButcherCarcassResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropKitchenBbqResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropKitchenOven4CookersResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropKitchenSinkResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropCanteenCakeFridgeResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropShoppingCartBlueFullResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropOldTvResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropJailLockerFullResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropMuseumLaserHorizontalResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropTrapdoorResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropTicketMachineResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropHospitalTvReportageResourcePath { get; private set; } = string.Empty;
        public static string InteriorRoomPropFishCuttingSinkResourcePath { get; private set; } = string.Empty;
        public static string VentIconResourcePath { get; private set; } = string.Empty;
        public static string KowloonVentIconResourcePath { get; private set; } = string.Empty;

        // ══════════════════════════════════════════════════════
        // VFX / 特效
        // ══════════════════════════════════════════════════════
        public static Sprite TaskGlow, BloodSplatter;

        // ══════════════════════════════════════════════════════
        // UI
        // ══════════════════════════════════════════════════════
        public static Sprite PanelBg, ButtonBg, AvatarFrame, VoteCardBg;

        private static bool _initialized;

        public static void Ensure()
        {
            if (_initialized) return;
            _initialized = true;

            // ─── 角色 ───
            GenerateAllCharacterSets();
            CharDirectionArrow = DrawArrow(16);
            CharGhostOverlay = DrawGhostOverlay(64);

            // ─── 地图 ─── (CC0优先，程序化兜底)
            FloorWood      = LoadCCTile("KowloonWalledCity/floors", "FloorWood") ?? DrawFloorWood(32);
            FloorConcrete  = LoadCCTile("PoliceStation/floors", "FloorConcrete") ?? DrawFloorConcrete(32);
            FloorMetal     = LoadCCTile("Harbour/floors", "FloorMetal")     ?? DrawFloorMetal(32);
            FloorCarpet    = LoadCCTile("KowloonWalledCity/floors", "FloorCarpet") ?? DrawFloorCarpet(32);
            FloorTile      = LoadCCTile("PoliceStation/floors", "FloorTile") ?? DrawFloorTileGrout(32);
            WallBrick      = LoadCCTile("KowloonWalledCity/walls", "WallBrick") ?? DrawWallBrick(16);
            WallConcrete   = LoadCCTile("PoliceStation/walls", "WallConcrete") ?? DrawWallConcrete(16);
            WallStripe     = LoadCCTile("Shared/walls", "WallStripe") ?? DrawWallStripe(16);
            FloorTileAlt   = LoadRuntimeTile(LimeZuInteriorFloorTilePath, out var floorTileAltPath) ?? FloorCarpet;
            FloorTileAltResourcePath = floorTileAltPath;
            CorridorTile   = LoadRuntimeTile(LimeZuExteriorFloorTilePath, out var corridorTilePath, 48f) ?? FloorConcrete;
            CorridorTileResourcePath = corridorTilePath;
            WallBlock      = LoadRuntimeTile(LimeZuInteriorWallTilePath, out var wallBlockPath, 16f) ?? WallBrick;
            WallBlockResourcePath = wallBlockPath;

            // ─── 道具 ───
            PropCrate      = LoadRuntimeTile(HarbourPropCrateWoodPath, out var propCratePath) ?? DrawCrate(32);
            PropCrateResourcePath = propCratePath;
            PropBarrel     = LoadRuntimeTile(HarbourPropBarrelOilPath, out var propBarrelPath) ?? DrawBarrel(32);
            PropBarrelResourcePath = propBarrelPath;
            PropKowloonCrate = LoadRuntimeTile(KowloonPropCrateOldPath, out var kowloonPropCratePath) ?? PropCrate;
            KowloonPropCrateResourcePath = kowloonPropCratePath;
            PropDesk       = LoadRuntimeTile(LimeZuOfficePropsTilePath, out var propDeskPath) ?? DrawDesk(32);
            PropDeskResourcePath = propDeskPath;
            PropCabinet    = LoadRuntimeTile(LimeZuOfficeWallPropsTilePath, out var propCabinetPath) ?? DrawCabinet(32);
            PropCabinetResourcePath = propCabinetPath;
            PropEvidenceBox= LoadRuntimeTile(LimeZuOfficeFloorPropsTilePath, out var propEvidenceBoxPath) ?? DrawEvidenceBox(32);
            PropEvidenceBoxResourcePath = propEvidenceBoxPath;
            LandmarkOfficeSign1 = LoadRuntimeTile(LimeZuLandmarkOfficeSign1Path, out var landmarkOfficeSign1Path) ?? PropCabinet;
            LandmarkOfficeSign1ResourcePath = landmarkOfficeSign1Path;
            LandmarkOfficeSign2 = LoadRuntimeTile(LimeZuLandmarkOfficeSign2Path, out var landmarkOfficeSign2Path) ?? PropCabinet;
            LandmarkOfficeSign2ResourcePath = landmarkOfficeSign2Path;
            LandmarkMailbox = LoadRuntimeTile(LimeZuLandmarkMailboxPath, out var landmarkMailboxPath) ?? PropEvidenceBox;
            LandmarkMailboxResourcePath = landmarkMailboxPath;
            LandmarkTruckFront = LoadRuntimeTile(LimeZuLandmarkTruckFrontPath, out var landmarkTruckFrontPath) ?? PropDesk;
            LandmarkTruckFrontResourcePath = landmarkTruckFrontPath;
            LandmarkUmbrella = LoadRuntimeTile(LimeZuLandmarkUmbrellaPath, out var landmarkUmbrellaPath) ?? PropDesk;
            LandmarkUmbrellaResourcePath = landmarkUmbrellaPath;
            LandmarkTinyTable = LoadRuntimeTile(LimeZuLandmarkTinyTablePath, out var landmarkTinyTablePath) ?? PropDesk;
            LandmarkTinyTableResourcePath = landmarkTinyTablePath;
            LandmarkPackage = LoadRuntimeTile(LimeZuLandmarkPackagePath, out var landmarkPackagePath) ?? PropEvidenceBox;
            LandmarkPackageResourcePath = landmarkPackagePath;
            LandmarkAirDuct = LoadRuntimeTile(LimeZuLandmarkAirDuctPath, out var landmarkAirDuctPath) ?? PropCabinet;
            LandmarkAirDuctResourcePath = landmarkAirDuctPath;
            LandmarkDoorOpen = LoadRuntimeTile(LimeZuLandmarkDoorOpenPath, out var landmarkDoorOpenPath) ?? PropCabinet;
            LandmarkDoorOpenResourcePath = landmarkDoorOpenPath;
            LandmarkPottedPlant = LoadRuntimeTile(LimeZuLandmarkPottedPlantPath, out var landmarkPottedPlantPath) ?? PropEvidenceBox;
            LandmarkPottedPlantResourcePath = landmarkPottedPlantPath;
            RoomPropGenerator = LoadRuntimeTile(LimeZuRoomPropGeneratorPath, out var roomPropGeneratorPath) ?? LandmarkAirDuct;
            RoomPropGeneratorResourcePath = roomPropGeneratorPath;
            RoomPropMonitor = LoadRuntimeTile(LimeZuRoomPropMonitorPath, out var roomPropMonitorPath) ?? PropDesk;
            RoomPropMonitorResourcePath = roomPropMonitorPath;
            RoomPropControlBigMonitor = LoadRuntimeTile(LimeZuRoomPropControlBigMonitorPath, out var roomPropControlBigMonitorPath) ?? PropDesk;
            RoomPropControlBigMonitorResourcePath = roomPropControlBigMonitorPath;
            RoomPropToolBox = LoadRuntimeTile(LimeZuRoomPropToolBoxPath, out var roomPropToolBoxPath) ?? PropEvidenceBox;
            RoomPropToolBoxResourcePath = roomPropToolBoxPath;
            RoomPropLightTower = LoadRuntimeTile(LimeZuRoomPropLightTowerPath, out var roomPropLightTowerPath) ?? LandmarkAirDuct;
            RoomPropLightTowerResourcePath = roomPropLightTowerPath;
            RoomPropSosBox = LoadRuntimeTile(LimeZuRoomPropSosBoxPath, out var roomPropSosBoxPath) ?? PropCabinet;
            RoomPropSosBoxResourcePath = roomPropSosBoxPath;
            RoomPropChair = LoadRuntimeTile(LimeZuRoomPropChairPath, out var roomPropChairPath) ?? PropDesk;
            RoomPropChairResourcePath = roomPropChairPath;
            RoomPropBenchedTable = LoadRuntimeTile(LimeZuRoomPropBenchedTablePath, out var roomPropBenchedTablePath) ?? PropDesk;
            RoomPropBenchedTableResourcePath = roomPropBenchedTablePath;
            RoomPropTrashCan = LoadRuntimeTile(LimeZuRoomPropTrashCanPath, out var roomPropTrashCanPath) ?? PropCabinet;
            RoomPropTrashCanResourcePath = roomPropTrashCanPath;
            RoomPropTrashPile = LoadRuntimeTile(LimeZuRoomPropTrashPilePath, out var roomPropTrashPilePath) ?? PropEvidenceBox;
            RoomPropTrashPileResourcePath = roomPropTrashPilePath;
            OfficeRoomPropWhiteboard = LoadRuntimeTile(LimeZuOfficeRoomPropWhiteboardPath, out var officeRoomPropWhiteboardPath) ?? PropDesk;
            OfficeRoomPropWhiteboardResourcePath = officeRoomPropWhiteboardPath;
            OfficeRoomPropChartBoard = LoadRuntimeTile(LimeZuOfficeRoomPropChartBoardPath, out var officeRoomPropChartBoardPath) ?? PropDesk;
            OfficeRoomPropChartBoardResourcePath = officeRoomPropChartBoardPath;
            OfficeRoomPropServerRack = LoadRuntimeTile(LimeZuOfficeRoomPropServerRackPath, out var officeRoomPropServerRackPath) ?? PropCabinet;
            OfficeRoomPropServerRackResourcePath = officeRoomPropServerRackPath;
            OfficeRoomPropPrinter = LoadRuntimeTile(LimeZuOfficeRoomPropPrinterPath, out var officeRoomPropPrinterPath) ?? PropDesk;
            OfficeRoomPropPrinterResourcePath = officeRoomPropPrinterPath;
            OfficeRoomPropCornerDesk = LoadRuntimeTile(LimeZuOfficeRoomPropCornerDeskPath, out var officeRoomPropCornerDeskPath) ?? PropDesk;
            OfficeRoomPropCornerDeskResourcePath = officeRoomPropCornerDeskPath;
            OfficeRoomPropDualMonitorDesk = LoadRuntimeTile(LimeZuOfficeRoomPropDualMonitorDeskPath, out var officeRoomPropDualMonitorDeskPath) ?? PropDesk;
            OfficeRoomPropDualMonitorDeskResourcePath = officeRoomPropDualMonitorDeskPath;
            OfficeRoomPropCctvCameraRig = LoadRuntimeTile(LimeZuOfficeRoomPropCctvCameraRigPath, out var officeRoomPropCctvCameraRigPath) ?? PropDesk;
            OfficeRoomPropCctvCameraRigResourcePath = officeRoomPropCctvCameraRigPath;
            OfficeRoomPropMedicalCart = LoadRuntimeTile(LimeZuOfficeRoomPropMedicalCartPath, out var officeRoomPropMedicalCartPath) ?? PropCabinet;
            OfficeRoomPropMedicalCartResourcePath = officeRoomPropMedicalCartPath;
            InteriorRoomPropHospitalResonanceMachine = LoadRuntimeTile(LimeZuInteriorRoomPropHospitalResonanceMachinePath, out var interiorRoomPropHospitalResonanceMachinePath) ?? PropCabinet;
            InteriorRoomPropHospitalResonanceMachineResourcePath = interiorRoomPropHospitalResonanceMachinePath;
            InteriorRoomPropHospitalScreenColor = LoadRuntimeTile(LimeZuInteriorRoomPropHospitalScreenColorPath, out var interiorRoomPropHospitalScreenColorPath) ?? RoomPropMonitor;
            InteriorRoomPropHospitalScreenColorResourcePath = interiorRoomPropHospitalScreenColorPath;
            InteriorRoomPropHospitalSink = LoadRuntimeTile(LimeZuInteriorRoomPropHospitalSinkPath, out var interiorRoomPropHospitalSinkPath) ?? PropCabinet;
            InteriorRoomPropHospitalSinkResourcePath = interiorRoomPropHospitalSinkPath;
            InteriorRoomPropHospitalXrayMachine = LoadRuntimeTile(LimeZuInteriorRoomPropHospitalXrayMachinePath, out var interiorRoomPropHospitalXrayMachinePath) ?? PropCabinet;
            InteriorRoomPropHospitalXrayMachineResourcePath = interiorRoomPropHospitalXrayMachinePath;
            InteriorRoomPropHospitalXrayScreen = LoadRuntimeTile(LimeZuInteriorRoomPropHospitalXrayScreenPath, out var interiorRoomPropHospitalXrayScreenPath) ?? RoomPropMonitor;
            InteriorRoomPropHospitalXrayScreenResourcePath = interiorRoomPropHospitalXrayScreenPath;
            InteriorRoomPropMorgueFreezerCorpseDoor = LoadRuntimeTile(LimeZuInteriorRoomPropMorgueFreezerCorpseDoorPath, out var interiorRoomPropMorgueFreezerCorpseDoorPath) ?? PropCabinet;
            InteriorRoomPropMorgueFreezerCorpseDoorResourcePath = interiorRoomPropMorgueFreezerCorpseDoorPath;
            InteriorRoomPropSecurityCameraWallRight = LoadRuntimeTile(LimeZuInteriorRoomPropSecurityCameraWallRightPath, out var interiorRoomPropSecurityCameraWallRightPath) ?? PropDesk;
            InteriorRoomPropSecurityCameraWallRightResourcePath = interiorRoomPropSecurityCameraWallRightPath;
            InteriorRoomPropSafeGold = LoadRuntimeTile(LimeZuInteriorRoomPropSafeGoldPath, out var interiorRoomPropSafeGoldPath) ?? PropEvidenceBox;
            InteriorRoomPropSafeGoldResourcePath = interiorRoomPropSafeGoldPath;
            InteriorRoomPropSafeBucks = LoadRuntimeTile(LimeZuInteriorRoomPropSafeBucksPath, out var interiorRoomPropSafeBucksPath) ?? PropEvidenceBox;
            InteriorRoomPropSafeBucksResourcePath = interiorRoomPropSafeBucksPath;
            InteriorRoomPropGroceryCheckoutRoller = LoadRuntimeTile(LimeZuInteriorRoomPropGroceryCheckoutRollerPath, out var interiorRoomPropGroceryCheckoutRollerPath) ?? PropDesk;
            InteriorRoomPropGroceryCheckoutRollerResourcePath = interiorRoomPropGroceryCheckoutRollerPath;
            InteriorRoomPropGroceryGlassFridge = LoadRuntimeTile(LimeZuInteriorRoomPropGroceryGlassFridgePath, out var interiorRoomPropGroceryGlassFridgePath) ?? PropCabinet;
            InteriorRoomPropGroceryGlassFridgeResourcePath = interiorRoomPropGroceryGlassFridgePath;
            InteriorRoomPropButcherCarcass = LoadRuntimeTile(LimeZuInteriorRoomPropButcherCarcassPath, out var interiorRoomPropButcherCarcassPath) ?? PropEvidenceBox;
            InteriorRoomPropButcherCarcassResourcePath = interiorRoomPropButcherCarcassPath;
            InteriorRoomPropKitchenBbq = LoadRuntimeTile(LimeZuInteriorRoomPropKitchenBbqPath, out var interiorRoomPropKitchenBbqPath) ?? PropCabinet;
            InteriorRoomPropKitchenBbqResourcePath = interiorRoomPropKitchenBbqPath;
            InteriorRoomPropKitchenOven4Cookers = LoadRuntimeTile(LimeZuInteriorRoomPropKitchenOven4CookersPath, out var interiorRoomPropKitchenOven4CookersPath) ?? PropCabinet;
            InteriorRoomPropKitchenOven4CookersResourcePath = interiorRoomPropKitchenOven4CookersPath;
            InteriorRoomPropKitchenSink = LoadRuntimeTile(LimeZuInteriorRoomPropKitchenSinkPath, out var interiorRoomPropKitchenSinkPath) ?? PropCabinet;
            InteriorRoomPropKitchenSinkResourcePath = interiorRoomPropKitchenSinkPath;
            InteriorRoomPropCanteenCakeFridge = LoadRuntimeTile(LimeZuInteriorRoomPropCanteenCakeFridgePath, out var interiorRoomPropCanteenCakeFridgePath) ?? PropCabinet;
            InteriorRoomPropCanteenCakeFridgeResourcePath = interiorRoomPropCanteenCakeFridgePath;
            InteriorRoomPropShoppingCartBlueFull = LoadRuntimeTile(LimeZuInteriorRoomPropShoppingCartBlueFullPath, out var interiorRoomPropShoppingCartBlueFullPath) ?? PropEvidenceBox;
            InteriorRoomPropShoppingCartBlueFullResourcePath = interiorRoomPropShoppingCartBlueFullPath;
            InteriorRoomPropOldTv = LoadRuntimeTile(LimeZuInteriorRoomPropOldTvPath, out var interiorRoomPropOldTvPath) ?? RoomPropMonitor;
            InteriorRoomPropOldTvResourcePath = interiorRoomPropOldTvPath;
            InteriorRoomPropJailLockerFull = LoadRuntimeTile(LimeZuInteriorRoomPropJailLockerFullPath, out var interiorRoomPropJailLockerFullPath) ?? PropCabinet;
            InteriorRoomPropJailLockerFullResourcePath = interiorRoomPropJailLockerFullPath;
            InteriorRoomPropMuseumLaserHorizontal = LoadRuntimeTile(LimeZuInteriorRoomPropMuseumLaserHorizontalPath, out var interiorRoomPropMuseumLaserHorizontalPath) ?? PropEvidenceBox;
            InteriorRoomPropMuseumLaserHorizontalResourcePath = interiorRoomPropMuseumLaserHorizontalPath;
            InteriorRoomPropTrapdoor = LoadRuntimeTile(LimeZuInteriorRoomPropTrapdoorPath, out var interiorRoomPropTrapdoorPath) ?? PropEvidenceBox;
            InteriorRoomPropTrapdoorResourcePath = interiorRoomPropTrapdoorPath;
            InteriorRoomPropTicketMachine = LoadRuntimeTile(LimeZuInteriorRoomPropTicketMachinePath, out var interiorRoomPropTicketMachinePath) ?? PropDesk;
            InteriorRoomPropTicketMachineResourcePath = interiorRoomPropTicketMachinePath;
            InteriorRoomPropHospitalTvReportage = LoadRuntimeTile(LimeZuInteriorRoomPropHospitalTvReportagePath, out var interiorRoomPropHospitalTvReportagePath) ?? RoomPropMonitor;
            InteriorRoomPropHospitalTvReportageResourcePath = interiorRoomPropHospitalTvReportagePath;
            InteriorRoomPropFishCuttingSink = LoadRuntimeTile(LimeZuInteriorRoomPropFishCuttingSinkPath, out var interiorRoomPropFishCuttingSinkPath) ?? PropCabinet;
            InteriorRoomPropFishCuttingSinkResourcePath = interiorRoomPropFishCuttingSinkPath;
            VentIcon       = LoadRuntimeTile(HarbourPropVentBackalleyPath, out var ventIconPath) ?? DrawVentGrate(32);
            VentIconResourcePath = ventIconPath;
            KowloonVentIcon = LoadRuntimeTile(KowloonPropVentRustPath, out var kowloonVentIconPath) ?? VentIcon;
            KowloonVentIconResourcePath = kowloonVentIconPath;
            CameraIcon     = DrawCameraHousing(32);

            // ─── VFX ───
            TaskGlow   = DrawGlowRing(32, new Color(0.15f, 0.65f, 1f));
            BloodSplatter = DrawBloodPool(32);
            CorpseMarker  = DrawCorpse(32);

            // ─── UI ───
            PanelBg    = DrawPanel(8, new Color(0.04f, 0.05f, 0.07f, 0.85f), new Color(0.25f, 0.28f, 0.32f, 1f));
            ButtonBg   = DrawPanel(8, new Color(0.10f, 0.13f, 0.18f, 1f), new Color(0.35f, 0.38f, 0.42f, 1f));
            AvatarFrame= DrawAvatarFrame(64);
            VoteCardBg = DrawPanel(4, new Color(0.08f, 0.09f, 0.12f, 0.9f), new Color(0.30f, 0.33f, 0.37f, 1f));
        }

        // ═══════════════════════════════════════════════════════════════
        // E2 职业角色生成器（64×64 像素，4向×3帧+死亡+头像）
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Try to load a CC0 pixel-art character set from Resources.
        /// Returns null if CC0 sprites are not available for this profession.
        /// Path convention: Resources/Sprites/Characters/{Profession}/{Direction}/{frame}.png
        /// </summary>
        private static ProfSpriteSet LoadCC0CharacterSet(Online.OnlineProfession prof)
        {
            string basePath = $"Sprites/Characters/{prof}";

            // Check if CC0 sprites exist for this profession
            var probe = Resources.Load<Texture2D>($"{basePath}/Front/idle");
            if (probe == null) return null;

            var set = new ProfSpriteSet();

            // Helper: load Texture2D, convert to Sprite (pixel-art, point filter)
            System.Func<string, Vector2, float, Sprite> LoadSprite = (path, pivot, pixelsPerUnit) =>
            {
                var tex = Resources.Load<Texture2D>(path);
                if (tex == null) return null;
                tex.filterMode = FilterMode.Point;
                return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    pivot, pixelsPerUnit);
            };

            // Frame mapping: CC0 idle→Frame0(站立), walk_0→Frame1, walk_1→Frame2
            set.Front_Frame0 = LoadSprite($"{basePath}/Front/idle", new Vector2(0.5f, 0f), 16f);
            set.Front_Frame1 = LoadSprite($"{basePath}/Front/walk_0", new Vector2(0.5f, 0f), 16f);
            set.Front_Frame2 = LoadSprite($"{basePath}/Front/walk_1", new Vector2(0.5f, 0f), 16f);

            set.Back_Frame0  = LoadSprite($"{basePath}/Back/idle", new Vector2(0.5f, 0f), 16f);
            set.Back_Frame1  = LoadSprite($"{basePath}/Back/walk_0", new Vector2(0.5f, 0f), 16f);
            set.Back_Frame2  = LoadSprite($"{basePath}/Back/walk_1", new Vector2(0.5f, 0f), 16f);

            set.Left_Frame0  = LoadSprite($"{basePath}/Left/idle", new Vector2(0.5f, 0f), 16f);
            set.Left_Frame1  = LoadSprite($"{basePath}/Left/walk_0", new Vector2(0.5f, 0f), 16f);
            set.Left_Frame2  = LoadSprite($"{basePath}/Left/walk_1", new Vector2(0.5f, 0f), 16f);

            set.Right_Frame0 = LoadSprite($"{basePath}/Right/idle", new Vector2(0.5f, 0f), 16f);
            set.Right_Frame1 = LoadSprite($"{basePath}/Right/walk_0", new Vector2(0.5f, 0f), 16f);
            set.Right_Frame2 = LoadSprite($"{basePath}/Right/walk_1", new Vector2(0.5f, 0f), 16f);

            set.Dead = LoadSprite($"{basePath}/death", new Vector2(0.5f, 0.5f), 64f);
            set.Avatar = LoadSprite($"{basePath}/avatar", new Vector2(0.5f, 0.5f), 32f);

            Debug.Log($"[Sprite2DAssetCache] Loaded CC0 pixel sprites for {prof}");

            return set;
        }

        /// <summary>
        /// Try to load a CC0 tileset sprite from Resources.
        /// Returns the first available PNG in the theme/category path.
        /// </summary>
        private static Sprite LoadCCTile(string resourceSubPath, string fallbackName)
        {
            string fullPath = $"Sprites/Tilesets/{resourceSubPath}";

            // Load all textures in this directory
            var textures = Resources.LoadAll<Texture2D>(fullPath);
            if (textures == null || textures.Length == 0) return null;

            // Pick the first one (representative tile)
            var tex = textures[0];
            tex.filterMode = FilterMode.Point;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f), Mathf.Max(tex.width, tex.height));
        }

        private static Sprite LoadRuntimeTile(string resourcePath, out string loadedResourcePath, float pixelsPerUnit = 0f)
        {
            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null)
            {
                loadedResourcePath = string.Empty;
                return null;
            }

            tex.filterMode = FilterMode.Point;
            loadedResourcePath = resourcePath;
            float ppu = pixelsPerUnit > 0f ? pixelsPerUnit : Mathf.Max(tex.width, tex.height);
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu);
        }

        private static void GenerateAllCharacterSets()
        {
            foreach (Online.OnlineProfession prof in System.Enum.GetValues(typeof(Online.OnlineProfession)))
            {
                // Try CC0 pixel art first, fall back to procedural
                var set = LoadCC0CharacterSet(prof);
                if (set != null)
                {
                    CharacterSets[prof] = set;
                    continue;
                }

                set = CreateProceduralCharacterSet(prof);
                CharacterSets[prof] = set;
            }

            var def = CharacterSets[Online.OnlineProfession.Inspector];
            CharBody_Front = def.Front_Frame0;
            CharBody_Back  = def.Back_Frame0;
            CharBody_Left  = def.Left_Frame0;
            CharBody_Right = def.Right_Frame0;
        }

        public static ProfSpriteSet CreateProceduralCharacterSet(Online.OnlineProfession prof)
        {
            Color main = ProfessionPalette.MainColor(prof);
            Color accent = ProfessionPalette.AccentColor(prof);
            var set = new ProfSpriteSet();

            // 正面：3帧行走（-1左腿前, 0站立, 1右腿前）
            set.Front_Frame0 = DrawCharFront(prof, main, accent, 0);
            set.Front_Frame1 = DrawCharFront(prof, main, accent, -1);
            set.Front_Frame2 = DrawCharFront(prof, main, accent, 1);
            // 背面：3帧行走
            set.Back_Frame0 = DrawCharBack(prof, main, accent, 0);
            set.Back_Frame1 = DrawCharBack(prof, main, accent, -1);
            set.Back_Frame2 = DrawCharBack(prof, main, accent, 1);
            // 侧面：左右各3帧
            set.Left_Frame0 = DrawCharSide(prof, main, accent, 0, true);
            set.Left_Frame1 = DrawCharSide(prof, main, accent, -1, true);
            set.Left_Frame2 = DrawCharSide(prof, main, accent, 1, true);
            set.Right_Frame0 = DrawCharSide(prof, main, accent, 0, false);
            set.Right_Frame1 = DrawCharSide(prof, main, accent, -1, false);
            set.Right_Frame2 = DrawCharSide(prof, main, accent, 1, false);
            // 死亡+头像
            set.Dead = DrawCharDead(prof, main);
            set.Avatar = DrawCharAvatar(prof, main, accent);

            return set;
        }

        // ═══════════════════════════════════════════════════
        // 职业专属头饰绘制
        // ═══════════════════════════════════════════════════
        static void DrawProfHeadwear(Texture2D t, Online.OnlineProfession prof, Color main, Color accent, int cx, bool isBack)
        {
            if (isBack)
            {
                switch (prof)
                {
                case Online.OnlineProfession.Inspector:
                    FillRect(t, cx-6, 2, 13, 5, main); break; // 警帽后
                case Online.OnlineProfession.Tech:
                    FillRect(t, cx-8, 3, 17, 4, main); break; // 安全帽
                case Online.OnlineProfession.Forensics:
                    FillRect(t, cx-5, 4, 11, 3, Color.white); break; // 白帽
                case Online.OnlineProfession.Enforcer:
                    FillRect(t, cx-6, 2, 13, 4, Dark(main, 0.3f)); break;
                case Online.OnlineProfession.Fixer:
                    FillRect(t, cx-9, 0, 19, 4, Dark(main, 0.5f)); FillRect(t, cx-7, 4, 15, 3, main); break; // 礼帽
                case Online.OnlineProfession.Driver:
                    FillRect(t, cx-6, 3, 13, 3, main); FillRect(t, cx-4, 0, 9, 3, accent); break; // 棒球帽
                case Online.OnlineProfession.UndercoverAgent:
                    FillRect(t, cx-5, 2, 11, 4, accent); break;
                case Online.OnlineProfession.Mole:
                    FillRect(t, cx-8, 2, 17, 4, Dark(main, 0.4f)); break;
                default:
                    FillRect(t, cx-6, 3, 13, 4, main); break;
                }
                return;
            }

            // 正面头饰
            switch (prof)
            {
            case Online.OnlineProfession.Inspector:
                // 警帽：宽帽檐+帽徽
                FillRect(t, cx-10, 1, 21, 4, Dark(main, 0.3f)); // 帽檐
                FillRect(t, cx-6, 4, 13, 5, main);               // 帽身
                FillRect(t, cx-2, 5, 5, 3, accent);              // 帽徽
                FillRect(t, cx-3, 3, 7, 1, accent);              // 帽徽横条
                break;
            case Online.OnlineProfession.Tech:
                // 安全帽+耳机
                FillRect(t, cx-8, 1, 17, 5, accent);             // 安全帽
                FillRect(t, cx-9, 8, 3, 3, Dark(main, 0.5f));    // 左耳机
                FillRect(t, cx+7, 8, 3, 3, Dark(main, 0.5f));    // 右耳机
                FillRect(t, cx-8, 11, 2, 5, Dark(main, 0.5f));   // 麦克风臂
                FillRect(t, cx-6, 14, 2, 2, Hex("#333333"));     // 麦克风
                break;
            case Online.OnlineProfession.Forensics:
                // 眼镜+白帽
                FillRect(t, cx-4, 6, 10, 2, Color.white);        // 帽
                FillRect(t, cx-6, 12, 3, 2, Hex("#444444"));     // 左镜框
                FillRect(t, cx+4, 12, 3, 2, Hex("#444444"));     // 右镜框
                FillRect(t, cx-1, 12, 3, 1, Hex("#444444"));     // 鼻梁
                break;
            case Online.OnlineProfession.Enforcer:
                // 寸头+伤疤
                FillRect(t, cx-5, 2, 11, 3, Dark(main, 0.6f));
                FillRect(t, cx-4, 8, 8, 1, Hex("#cc3333"));      // 伤痕
                break;
            case Online.OnlineProfession.Fixer:
                // 礼帽+墨镜
                FillRect(t, cx-10, 0, 21, 3, Dark(main, 0.6f));  // 帽檐
                FillRect(t, cx-6, 3, 13, 4, main);               // 帽身
                FillRect(t, cx-5, 9, 11, 2, Hex("#111111"));     // 墨镜
                break;
            case Online.OnlineProfession.UndercoverAgent:
                // 半遮面罩
                FillRect(t, cx-5, 6, 11, 4, accent);
                FillRect(t, cx-6, 10, 13, 2, Dark(accent, 0.3f));
                break;
            case Online.OnlineProfession.Driver:
                // 棒球帽
                FillRect(t, cx-7, 1, 15, 3, main);
                FillRect(t, cx-5, 4, 11, 3, accent);
                FillRect(t, cx+4, 0, 5, 4, main);                // 帽舌
                break;
            case Online.OnlineProfession.Mole:
                // 兜帽
                FillRect(t, cx-7, 0, 15, 7, Dark(main, 0.5f));
                FillRect(t, cx-2, 7, 5, 2, accent);
                break;
            default:
                FillRect(t, cx-6, 3, 13, 4, main);
                break;
            }
        }

        // ═══════════════════════════════════════════════════
        // 职业专属制服细节（正面）
        // ═══════════════════════════════════════════════════
        static void DrawProfUniformFront(Texture2D t, Online.OnlineProfession prof, Color main, Color accent, int cx)
        {
            switch (prof)
            {
            case Online.OnlineProfession.Inspector:
                // 警服：领带+警徽
                FillRect(t, cx-1, 20, 3, 8, accent);             // 领带
                FillRect(t, cx-2, 18, 5, 2, Color.white);        // 领口
                FillRect(t, cx-3, 28, 7, 4, Hex("#ffd700"));     // 警徽
                FillRect(t, cx-1, 29, 3, 2, Dark(Hex("#ffd700"), 0.3f)); // 警徽细节
                break;
            case Online.OnlineProfession.Tech:
                // 工具背心
                FillRect(t, cx-7, 18, 15, 2, accent);            // 肩带
                FillRect(t, cx-6, 28, 4, 3, Hex("#555555"));     // 左工具袋
                FillRect(t, cx+3, 28, 4, 3, Hex("#555555"));     // 右工具袋
                FillRect(t, cx+4, 31, 2, 2, Hex("#ff4444"));     // 红灯
                break;
            case Online.OnlineProfession.Forensics:
                // 白大褂
                FillRect(t, cx-7, 18, 15, 1, Color.white);       // 领口白边
                FillRect(t, cx-6, 25, 13, 6, Color.white);       // 白大褂
                FillRect(t, cx-3, 28, 7, 4, Hex("#2e8b57"));     // 证件
                break;
            case Online.OnlineProfession.Enforcer:
                // 肌肉线条
                FillRect(t, cx-3, 18, 7, 2, Dark(main, 0.3f));   // 领口深色
                FillRect(t, cx-8, 28, 3, 5, Color.black);        // 左臂带
                FillRect(t, cx+6, 28, 3, 5, Color.black);        // 右臂带
                FillRect(t, cx-2, 30, 5, 1, Hex("#ff3333"));     // 红线
                break;
            case Online.OnlineProfession.Fixer:
                // 西装马甲
                FillRect(t, cx-2, 19, 5, 8, Color.white);        // 衬衫
                FillRect(t, cx-7, 20, 3, 10, Dark(main, 0.2f));  // 左西装
                FillRect(t, cx+5, 20, 3, 10, Dark(main, 0.2f));  // 右西装
                FillRect(t, cx-1, 27, 3, 2, Hex("#ffd700"));     // 扣子
                break;
            case Online.OnlineProfession.UndercoverAgent:
                // 普通衣服+隐藏设备
                FillRect(t, cx-5, 20, 11, 1, accent);            // 领口
                FillRect(t, cx+3, 28, 3, 3, Dark(accent, 0.3f)); // 隐藏口袋
                break;
            case Online.OnlineProfession.Driver:
                // 夹克
                FillRect(t, cx-6, 18, 13, 1, accent);            // 领口
                FillRect(t, cx-7, 27, 3, 5, Dark(main, 0.3f));   // 左口袋
                FillRect(t, cx+5, 27, 3, 5, Dark(main, 0.3f));   // 右口袋
                break;
            case Online.OnlineProfession.Mole:
                // 藏匿武器
                FillRect(t, cx-7, 20, 3, 10, Dark(main, 0.4f));
                FillRect(t, cx+6, 26, 2, 6, Hex("#555555"));     // 隐藏刀柄
                break;
            }
        }

        // ═══════════════════════════════════════════════════
        // E2 正面角色（64×64，含职业细节）
        // ═══════════════════════════════════════════════════
        static Sprite DrawCharFront(Online.OnlineProfession prof, Color body, Color accent, int walkOffset)
        {
            var t = NewTex(64); int cx = 32, cy = 12;
            Color dark  = Dark(body, 0.4f);
            Color dark2 = Dark(body, 0.6f);
            Color skin  = Hex("#e8c39e");
            Color skinShadow = Dark(skin, 0.2f);

            // ── 头（圆形+肤色）──
            FillCircle(t, cx, cy, 8, skin);
            FillCircle(t, cx, cy-1, 6, skin);
            // 眼睛
            FillRect(t, cx-4, cy-1, 2, 2, Hex("#2b2118"));
            FillRect(t, cx+3, cy-1, 2, 2, Hex("#2b2118"));
            // 嘴
            FillRect(t, cx-1, cy+3, 3, 1, Hex("#c4956a"));

            // ── 职业头饰 ──
            DrawProfHeadwear(t, prof, body, accent, cx, false);

            // ── 脖子 ──
            FillRect(t, cx-2, 18, 5, 3, skinShadow);

            // ── 身体（梯形：上窄下宽）──
            FillRect(t, cx-7, 20, 4, 18, dark);
            FillRect(t, cx-3, 20, 7, 18, body);
            FillRect(t, cx+4, 20, 4, 18, dark);
            // 身体阴影边缘
            FillRect(t, cx-7, 20, 1, 18, dark2);
            FillRect(t, cx+7, 20, 1, 18, dark2);

            // ── 职业制服细节 ──
            DrawProfUniformFront(t, prof, body, accent, cx);

            // ── 腰带 ──
            FillRect(t, cx-7, 35, 15, 3, accent);
            FillRect(t, cx-1, 35, 3, 3, Dark(accent, 0.4f)); // 腰带扣

            // ── 手臂（带行走摆动）──
            int armSwingL = walkOffset < 0 ? -2 : walkOffset > 0 ? 2 : 0;
            int armSwingR = walkOffset < 0 ? 2 : walkOffset > 0 ? -2 : 0;
            // 左臂
            FillRect(t, cx-11, 20, 3, 12, dark);
            FillRect(t, cx-11+armSwingL, 20, 1, 12, dark2); // 阴影
            FillRect(t, cx-12, 30+armSwingL, 5, 4, skin);    // 左手
            // 右臂
            FillRect(t, cx+9, 20, 3, 12, dark);
            FillRect(t, cx+11+armSwingR, 20, 1, 12, dark2);
            FillRect(t, cx+8, 30+armSwingR, 5, 4, skin);     // 右手

            // ── 腿（带行走摆动）──
            int legSep = walkOffset * 2;
            // 左腿
            FillRect(t, cx-6 + legSep, 38, 5, 12, dark);
            FillRect(t, cx-6 + legSep, 38, 1, 12, dark2);
            // 右腿
            FillRect(t, cx+2 - legSep, 38, 5, 12, dark);
            FillRect(t, cx+2 - legSep, 38, 1, 12, dark2);

            // ── 靴子 ──
            Color boot = Dark(body, 0.7f);
            FillRect(t, cx-7 + legSep, 49, 7, 3, boot);
            FillRect(t, cx+1 - legSep, 49, 7, 3, boot);
            // 靴底
            FillRect(t, cx-7 + legSep, 52, 7, 2, Color.black);
            FillRect(t, cx+1 - legSep, 52, 7, 2, Color.black);

            // ── 职业手持物（仅站立帧显示）──
            if (walkOffset == 0) DrawProfHeldItem(t, prof, body, accent, cx);

            t.Apply(); return Spr(t, 64);
        }

        static void DrawProfHeldItem(Texture2D t, Online.OnlineProfession prof, Color body, Color accent, int cx)
        {
            switch (prof)
            {
            case Online.OnlineProfession.Inspector:
                FillRect(t, cx+10, 27, 2, 6, Hex("#ffd700")); break; // 手铐
            case Online.OnlineProfession.Tech:
                FillRect(t, cx+10, 28, 3, 5, Hex("#666666")); break; // 扳手
            case Online.OnlineProfession.Forensics:
                FillRect(t, cx+10, 26, 4, 5, Color.white); break;    // 证据袋
            case Online.OnlineProfession.Enforcer:
                FillRect(t, cx+10, 26, 2, 8, Hex("#555555")); break; // 警棍
            }
        }

        // ═══════════════════════════════════════════════════
        // E2 背面角色（64×64）
        // ═══════════════════════════════════════════════════
        static Sprite DrawCharBack(Online.OnlineProfession prof, Color body, Color accent, int walkOffset)
        {
            var t = NewTex(64); int cx = 32;
            Color dark  = Dark(body, 0.4f);
            Color dark2 = Dark(body, 0.6f);

            // 头后
            FillCircle(t, cx, 12, 8, body);
            FillCircle(t, cx, 11, 6, Dark(body, 0.2f));
            // 职业头饰
            DrawProfHeadwear(t, prof, body, accent, cx, true);

            // 脖子
            FillRect(t, cx-2, 18, 5, 3, Dark(body, 0.3f));

            // 身体（背面统一深色）
            FillRect(t, cx-7, 20, 4, 18, dark2);
            FillRect(t, cx-3, 20, 7, 18, dark);
            FillRect(t, cx+4, 20, 4, 18, dark2);

            // 腰带
            FillRect(t, cx-7, 35, 15, 3, accent);
            FillRect(t, cx-1, 35, 3, 3, Dark(accent, 0.4f));

            // 手臂
            int swing = walkOffset;
            FillRect(t, cx-11+swing, 20, 3, 14, dark2);
            FillRect(t, cx+9-swing, 20, 3, 14, dark2);

            // 腿
            int l = walkOffset * 2;
            FillRect(t, cx-6 + l, 38, 5, 12, dark2);
            FillRect(t, cx+2 - l, 38, 5, 12, dark2);

            // 靴子
            Color boot = Dark(body, 0.7f);
            FillRect(t, cx-7 + l, 49, 7, 3, boot);
            FillRect(t, cx+1 - l, 49, 7, 3, boot);
            FillRect(t, cx-7 + l, 52, 7, 2, Color.black);
            FillRect(t, cx+1 - l, 52, 7, 2, Color.black);

            t.Apply(); return Spr(t, 64);
        }

        // ═══════════════════════════════════════════════════
        // E2 侧面角色（64×64，职业专属轮廓）
        // ═══════════════════════════════════════════════════
        static Sprite DrawCharSide(Online.OnlineProfession prof, Color body, Color accent, int walkOffset, bool facingLeft)
        {
            var t = NewTex(64); int cx = 32;
            Color dark  = Dark(body, 0.4f);
            Color dark2 = Dark(body, 0.6f);
            Color skin  = Hex("#e8c39e");
            int dir = facingLeft ? -1 : 1;

            // ── 头（侧面）──
            FillCircle(t, cx, 12, 8, skin);
            FillCircle(t, cx+dir, 10, 5, skin);                    // 面部突出
            FillCircle(t, cx+dir*3, 10, 3, skin);                  // 鼻子
            // 眼
            FillRect(t, cx+dir*2, 10, 2, 1, Hex("#2b2118"));
            // 嘴
            FillRect(t, cx+dir, 14, 2, 1, Hex("#c4956a"));

            // ── 职业头饰（侧面）──
            if (prof == Online.OnlineProfession.Inspector)
            {
                FillRect(t, cx-5, 2, 14, 4, Dark(body, 0.3f));    // 帽檐
                FillRect(t, cx-4, 5, 12, 4, body);                 // 帽身
                FillRect(t, cx+dir*4, 2, 5, 2, accent);            // 帽徽
            }
            else if (prof == Online.OnlineProfession.Fixer)
            {
                FillRect(t, cx-9, 0, 20, 3, Dark(body, 0.6f));    // 帽檐
                FillRect(t, cx-5, 3, 12, 4, body);
            }
            else if (prof == Online.OnlineProfession.Driver)
            {
                FillRect(t, cx-4, 1, 10, 3, body);
                FillRect(t, cx+dir*3, 0, 6, 3, body);              // 帽舌
            }
            else
            {
                FillRect(t, cx-5, 3, 12, 4, body);
            }

            // ── 脖子 ──
            FillRect(t, cx+dir, 18, 4, 3, Dark(skin, 0.2f));

            // ── 身体（侧面比正面窄）──
            FillRect(t, cx-4, 20, 4, 18, dark2);                  // 后半身
            FillRect(t, cx,   20, 5, 18, body);                   // 前半身
            FillRect(t, cx+5, 20, 1, 18, dark);                   // 前边缘

            // ── 腰带 ──
            FillRect(t, cx-5, 35, 11, 3, accent);

            // ── 前臂（伸向移动方向）──
            int armExt = walkOffset;
            if (facingLeft)
            {
                FillRect(t, cx-11-armExt, 20, 4, 12, dark);       // 后臂
                FillRect(t, cx+6+armExt,  20, 3, 12, dark);       // 前臂
                FillRect(t, cx+7+armExt,  30, 4, 4, skin);        // 手
            }
            else
            {
                FillRect(t, cx-11+armExt, 20, 4, 12, dark);       // 后臂
                FillRect(t, cx+6-armExt,  20, 3, 12, dark);       // 前臂
                FillRect(t, cx+7-armExt,  30, 4, 4, skin);        // 手
            }

            // ── 腿（侧面一前一后）──
            int l = walkOffset * 2;
            FillRect(t, cx-2 + l, 38, 5, 12, dark2);             // 后腿
            FillRect(t, cx+2 - l, 38, 5, 12, dark);              // 前腿

            // ── 靴子 ──
            Color boot = Dark(body, 0.7f);
            FillRect(t, cx-3 + l, 49, 7, 3, boot);
            FillRect(t, cx+1 - l, 49, 7, 3, boot);
            FillRect(t, cx-3 + l, 52, 7, 2, Color.black);
            FillRect(t, cx+1 - l, 52, 7, 2, Color.black);

            t.Apply(); return Spr(t, 64);
        }

        // ═══════════════════════════════════════════════════
        // E2 死亡/尸体（职业专属）
        // ═══════════════════════════════════════════════════
        static Sprite DrawCharDead(Online.OnlineProfession prof, Color body)
        {
            var t = NewTex(64);
            Color dark  = Dark(body, 0.5f);
            Color blood = Hex("#8b0000");
            Color skin  = Hex("#e8c39e");
            Color boot  = Dark(body, 0.7f);

            // 身体横躺
            FillRect(t, 10, 28, 44, 10, dark);
            FillRect(t, 12, 26, 40, 3, body);

            // 头部（右侧）
            FillCircle(t, 50, 30, 7, skin);
            FillCircle(t, 52, 28, 4, skin);

            // 职业制服标识（保持可辨识）
            FillRect(t, 30, 32, 20, 3, ProfessionPalette.AccentColor(prof));

            // 靴子（左侧）
            FillRect(t, 10, 30, 8, 4, boot);

            // 血迹（散布在身体周围）
            for (int i = 0; i < 60; i++)
            {
                int x = RandomRange(8, 57, i);
                int y = RandomRange(24, 40, i + 13);
                if (t.GetPixel(x, y).a > 0.05f && t.GetPixel(x, y).a < 0.9f)
                    t.SetPixel(x, y, blood);
            }
            // 血泊
            for (int i = 0; i < 30; i++)
            {
                int x = RandomRange(20, 45, i + 37);
                int y = RandomRange(36, 48, i + 41);
                FillRect(t, x, y, 2, 2, new Color(blood.r, blood.g, blood.b, 0.5f));
            }

            t.Apply(); return Spr(t, 64);
        }

        // ═══════════════════════════════════════════════════
        // E2 会议头像（32×32，圆框内）
        // ═══════════════════════════════════════════════════
        static Sprite DrawCharAvatar(Online.OnlineProfession prof, Color body, Color accent)
        {
            var t = NewTex(32); int cx = 16, cy = 14;
            Color skin = Hex("#e8c39e");

            // 圆形头像背景
            FillCircle(t, cx, cy, 14, Dark(body, 0.3f));
            FillCircle(t, cx, cy, 12, body);

            // 头
            FillCircle(t, cx, cy-4, 7, skin);
            // 眼
            FillRect(t, cx-3, cy-6, 2, 1, Hex("#2b2118"));
            FillRect(t, cx+2, cy-6, 2, 1, Hex("#2b2118"));

            // 职业标识色条
            FillRect(t, cx-6, cy+2, 13, 3, accent);

            // 身体
            FillRect(t, cx-5, cy+5, 11, 6, body);

            // 圆框
            for (int x = 0; x < 32; x++)
                for (int y = 0; y < 32; y++)
                {
                    float d = Mathf.Sqrt((x-cx)*(x-cx) + (y-cy)*(y-cy));
                    if (d > 14 && d < 16)
                        t.SetPixel(x, y, accent);
                }

            t.Apply(); return Spr(t, 32, 32);
        }

        // ═══════════════════════════════════════════════════════════════
        // 地图 Tile 生成器（32×32）
        // ═══════════════════════════════════════════════════════════════

        static Sprite DrawFloorWood(int sz)
        {
            var t = NewTex(sz); Color a = Hex("#6b4226"), b = Hex("#7a5236"), c = Hex("#5c3618");
            for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                int plankY = y / 8;
                Color col = plankY % 3 == 0 ? a : plankY % 3 == 1 ? b : c;
                if (y % 8 < 2) col = Dark(col, 0.3f); // plank gap
                // wood grain noise
                float n = Mathf.PerlinNoise(x * 0.3f, y * 2f);
                if (n > 0.55f) col = Dark(col, 0.08f);
                t.SetPixel(x, y, col);
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawFloorConcrete(int sz)
        {
            var t = NewTex(sz); Color baseC = Hex("#8a8d91");
            for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.4f, y * 0.4f);
                Color col = n < 0.4f ? Dark(baseC, 0.1f) : n > 0.6f ? Light(baseC, 0.1f) : baseC;
                // small crack
                if (x == 10 && y > 14 && y < 22) col = Dark(col, 0.3f);
                if (x == 11 && y > 15 && y < 20) col = Dark(col, 0.2f);
                t.SetPixel(x, y, col);
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawFloorMetal(int sz)
        {
            var t = NewTex(sz); Color baseC = Hex("#708090");
            for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                bool grid = (x % 8 == 0 || y % 8 == 0);
                float n = Mathf.PerlinNoise(x * 0.2f, y * 0.2f);
                Color col = grid ? Dark(baseC, 0.25f) : baseC;
                col = n > 0.5f ? Light(col, 0.05f) : Dark(col, 0.05f);
                t.SetPixel(x, y, col);
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawFloorCarpet(int sz)
        {
            var t = NewTex(sz); Color baseC = Hex("#2d4a6e"), stripeC = Hex("#3a5f8a");
            for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                bool stripe = (y / 4) % 4 < 2;
                float n = Mathf.PerlinNoise(x * 0.5f, y * 0.5f);
                Color col = stripe ? baseC : stripeC;
                t.SetPixel(x, y, col);
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawFloorTileGrout(int sz)
        {
            var t = NewTex(sz); Color tileC = Hex("#d4c9a8"), grout = Hex("#8a8578");
            for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                bool isGrout = (x % 8 < 1 || y % 8 < 1);
                t.SetPixel(x, y, isGrout ? grout : tileC);
            }
            t.Apply(); return Spr(t, sz);
        }

        // ─── 墙壁 ───

        static Sprite DrawWallBrick(int sz)
        {
            var t = NewTex(sz); Color brick = Hex("#8b4513"), mortar = Hex("#4a3728");
            int bh = 4;
            for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                int row = y / bh; int off = (row % 2) * (sz / 4);
                int brickX = (x + off) % (sz / 2);
                bool isMortar = (y % bh == bh - 1) || brickX < 1 || brickX >= sz / 2 - 1;
                t.SetPixel(x, y, isMortar ? mortar : brick);
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawWallConcrete(int sz)
        {
            var t = NewTex(sz); Color c = Hex("#9a9ea3");
            for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                float n = Mathf.PerlinNoise(x * 0.3f, y * 0.3f);
                t.SetPixel(x, y, n > 0.5f ? Light(c, 0.05f) : Dark(c, 0.05f));
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawWallStripe(int sz)
        {
            var t = NewTex(sz); Color a = Hex("#2a2a2a"), b = Hex("#ffaa00");
            for (int y = 0; y < sz; y++)
            for (int x = 0; x < sz; x++)
            {
                bool stripe = (y / 4) % 6 < 1;
                t.SetPixel(x, y, stripe ? b : a);
            }
            t.Apply(); return Spr(t, sz);
        }

        // ═══════════════════════════════════════════════════════════════
        // 道具
        // ═══════════════════════════════════════════════════════════════

        static Sprite DrawCrate(int sz)
        {
            var t = NewTex(sz); Color wood = Hex("#8b6914"), line = Hex("#5c4510");
            FillRect(t, 6, 6, 20, 18, wood);
            FillRect(t, 6, 6, 20, 1, line);
            FillRect(t, 6, 23, 20, 1, line);
            FillRect(t, 15, 6, 1, 18, line);
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawBarrel(int sz)
        {
            var t = NewTex(sz); Color body = Hex("#6b3a1f"), rim = Hex("#8b5a30");
            FillRect(t, 8, 4, 16, 24, body);
            FillRect(t, 6, 3, 20, 3, rim);
            FillRect(t, 6, 26, 20, 3, rim);
            FillRect(t, 10, 12, 1, 8, rim);
            FillRect(t, 18, 12, 1, 8, rim);
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawDesk(int sz)
        {
            var t = NewTex(sz); Color top = Hex("#5c4033"), leg = Hex("#3d2b1f");
            FillRect(t, 2, 3, 28, 5, top);
            FillRect(t, 4, 8, 3, 20, leg);
            FillRect(t, 25, 8, 3, 20, leg);
            FillRect(t, 2, 26, 28, 3, top);
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawCabinet(int sz)
        {
            var t = NewTex(sz); Color body = Hex("#708090"), handle = Hex("#d4d4d4");
            FillRect(t, 3, 0, 26, 28, body);
            FillRect(t, 3, 13, 26, 2, Dark(body, 0.3f));
            FillRect(t, 24, 5, 3, 1, handle);
            FillRect(t, 24, 17, 3, 1, handle);
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawEvidenceBox(int sz)
        {
            var t = NewTex(sz); Color box = Hex("#c4a43c"), label = Hex("#ffffff");
            FillRect(t, 4, 4, 24, 22, box);
            FillRect(t, 7, 13, 18, 5, label);
            FillRect(t, 12, 14, 2, 3, Color.red);
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawVentGrate(int sz)
        {
            var t = NewTex(sz); Color frame = Hex("#4a4a4a"), dark = Hex("#1a1a1a");
            FillRect(t, 0, 0, sz, sz, frame);
            for (int x = 3; x < sz-3; x+=4)
                FillRect(t, x, 3, 2, sz-6, dark);
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawCameraHousing(int sz)
        {
            var t = NewTex(sz); Color body = Hex("#3a3a3a"), lens = Hex("#1a5a8a"), glow = Hex("#00aaff");
            FillRect(t, 6, 2, 20, 10, body);
            FillCircle(t, 16, 18, 12, body);
            FillCircle(t, 16, 18, 8, lens);
            FillCircle(t, 16, 18, 3, glow);
            t.Apply(); return Spr(t, sz);
        }

        // ═══════════════════════════════════════════════════════════════
        // VFX
        // ═══════════════════════════════════════════════════════════════

        static Sprite DrawGlowRing(int sz, Color glow)
        {
            var t = NewTex(sz); float cx = sz/2f, inner = cx*0.5f, outer = cx*0.95f;
            for (int y=0;y<sz;y++) for (int x=0;x<sz;x++)
            {
                float d = Mathf.Sqrt((x-cx)*(x-cx)+(y-cx)*(y-cx));
                float a = (d>inner && d<outer) ? 1f - Mathf.Abs(d-(inner+outer)*0.5f)/((outer-inner)*0.5f) : 0f;
                t.SetPixel(x,y,new Color(glow.r,glow.g,glow.b,a*0.5f));
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawBloodPool(int sz)
        {
            var t = NewTex(sz); Color blood = Hex("#8b0000"), clear = Color.clear;
            for (int y=0;y<sz;y++) for (int x=0;x<sz;x++)
            {
                float dx=x-sz/2f,dy=y-sz/2f,d=Mathf.Sqrt(dx*dx+dy*dy);
                float n=Mathf.PerlinNoise(x*0.3f,y*0.3f);
                float a=(d<sz*0.35f&&n>0.3f)?0.7f:0f;
                t.SetPixel(x,y,new Color(blood.r,blood.g,blood.b,a));
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawCorpse(int sz)
        {
            var t = NewTex(sz); Color red = Hex("#cc0000");
            FillCircle(t,sz/2,sz/2,sz/2-2, new Color(1,1,1,0));
            // X mark
            for (int i=-sz/3;i<sz/3;i++) { SetPixelSafe(t,sz/2+i,sz/2+i,red); SetPixelSafe(t,sz/2+i,sz/2-i,red); }
            t.Apply(); return Spr(t, sz);
        }

        // ═══════════════════════════════════════════════════════════════
        // UI
        // ═══════════════════════════════════════════════════════════════

        static Sprite DrawPanel(int sz, Color fill, Color border)
        {
            var t = NewTex(sz);
            for (int y=0;y<sz;y++) for (int x=0;x<sz;x++)
            {
                bool isBorder = x < 1 || x >= sz-1 || y < 1 || y >= sz-1;
                t.SetPixel(x, y, isBorder ? border : fill);
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawAvatarFrame(int sz)
        {
            var t = NewTex(sz); Color frame = Hex("#f4a236");
            for (int y=0;y<sz;y++) for (int x=0;x<sz;x++)
            {
                float dx=x-sz/2f, dy=y-sz/2f, d=Mathf.Sqrt(dx*dx+dy*dy);
                bool onRing = d>sz*0.42f && d<sz*0.5f;
                t.SetPixel(x,y,onRing?frame:Color.clear);
            }
            t.Apply(); return Spr(t, sz);
        }

        static Sprite DrawGhostOverlay(int sz)
        {
            var t = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
            for (int y=0;y<sz;y++) for (int x=0;x<sz;x++)
                t.SetPixel(x,y,new Color(0.5f,0.5f,0.6f,0.25f));
            t.Apply(); return Sprite.Create(t,new Rect(0,0,sz,sz),new Vector2(0.5f,0.5f),sz);
        }

        static Sprite DrawArrow(int sz)
        {
            var t = NewTex(sz); Color fill = Color.white; int cx = sz/2;
            for (int y=0;y<sz;y++) for (int x=0;x<sz;x++)
            {
                float dx = Mathf.Abs(x-cx); bool inArrow = y>=sz*0.15f && dx < y*0.5f;
                t.SetPixel(x,y,inArrow?fill:Color.clear);
            }
            t.Apply(); return Spr(t, sz);
        }

        // ═══════════════════════════════════════════════════════════════
        // 基础绘图工具
        // ═══════════════════════════════════════════════════════════════

        static Texture2D NewTex(int sz) => new Texture2D(sz, sz, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        static Sprite Spr(Texture2D t, int ppu) => Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), ppu);
        static Sprite Spr(Texture2D t, int w, int ppu) => Sprite.Create(t, new Rect(0, 0, w, w), new Vector2(0.5f, 0.5f), ppu);

        static void FillRect(Texture2D t, int x, int y, int w, int h, Color c)
        { for (int j=y;j<y+h&&j<t.height;j++) for (int i=x;i<x+w&&i<t.width;i++) if(i>=0&&j>=0)t.SetPixel(i,j,c); }

        static void FillCircle(Texture2D t, int cx, int cy, int r, Color c)
        { for (int y=cy-r;y<cy+r;y++) for (int x=cx-r;x<cx+r;x++)
        { if(x>=0&&y>=0&&x<t.width&&y<t.height && (x-cx)*(x-cx)+(y-cy)*(y-cy)<=r*r) t.SetPixel(x,y,c); }}

        static void SetPixelSafe(Texture2D t, int x, int y, Color c)
        { if(x>=0&&y>=0&&x<t.width&&y<t.height)t.SetPixel(x,y,c); }

        static Color Dark(Color c, float amt) => new Color(c.r*(1-amt), c.g*(1-amt), c.b*(1-amt), c.a);
        static Color Light(Color c, float amt) => new Color(c.r*(1+amt), c.g*(1+amt), c.b*(1+amt), c.a);
        static Color Hex(string h) { if(h.StartsWith("#"))h=h.Substring(1);
            return new Color(int.Parse(h.Substring(0,2),System.Globalization.NumberStyles.HexNumber)/255f,
                             int.Parse(h.Substring(2,2),System.Globalization.NumberStyles.HexNumber)/255f,
                             int.Parse(h.Substring(4,2),System.Globalization.NumberStyles.HexNumber)/255f,1f); }
        static int RandomRange(int min, int max, int seed) => min + (new System.Random(seed).Next() % (max-min));

        public class ProfSpriteSet
        {
            public Sprite Front_Frame0, Front_Frame1, Front_Frame2;
            public Sprite Back_Frame0, Back_Frame1, Back_Frame2;
            public Sprite Left_Frame0, Left_Frame1, Left_Frame2;
            public Sprite Right_Frame0, Right_Frame1, Right_Frame2;
            public Sprite Dead;
            public Sprite Avatar;
        }
    }
}
