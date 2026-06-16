using UnityEngine;

namespace GanglandUndercover.Online
{
    /// <summary>
    /// Map data service abstraction for the online match.
    /// Extracted from <see cref="OnlineMapService"/> to enable testing and alternative map providers.
    /// Covers coordinate conversion, spawn/task/passage positions, room definitions, and surveillance zones.
    /// </summary>
    public interface IMapService
    {
        // ================================================================
        //  Map Configuration
        // ================================================================

        /// <summary>Currently active map type (Harbour, Police Station, Kowloon Walled City).</summary>
        OnlineMapService.OnlineMapType ActiveMapType { get; set; }

        /// <summary>World-coordinate half-width of the map.</summary>
        float MapHalfWidth { get; }

        /// <summary>World-coordinate half-height of the map.</summary>
        float MapHalfHeight { get; }

        /// <summary>Design-to-world scale factor on the X axis.</summary>
        float DesignScaleX { get; }

        /// <summary>Design-to-world scale factor on the Y axis.</summary>
        float DesignScaleY { get; }

        /// <summary>Design-coordinate half-width of the map.</summary>
        float DesignMapHalfWidth { get; }

        /// <summary>Design-coordinate half-height of the map.</summary>
        float DesignMapHalfHeight { get; }

        // ================================================================
        //  Coordinate Conversion
        // ================================================================

        /// <summary>Convert a design-space position to world coordinates.</summary>
        Vector3 ScaleMapPosition(Vector3 designPosition);

        /// <summary>Convert a design-space size to world coordinates.</summary>
        Vector3 ScaleMapSize(Vector3 designSize);

        /// <summary>Clamp a world position to the online map boundary.</summary>
        Vector3 ClampToOnlineMap(Vector3 position);

        /// <summary>Clamp a design position to the design map boundary.</summary>
        Vector3 ClampToDesignMap(Vector3 position);

        // ================================================================
        //  Spawn Points
        // ================================================================

        /// <summary>Get the world-coordinate spawn position for the given index (cyclic).</summary>
        Vector3 SpawnPosition(int index);

        // ================================================================
        //  Task Stations
        // ================================================================

        /// <summary>Get the world-coordinate position of a task station by task ID.</summary>
        Vector3 TaskPositionFor(int id);

        // ================================================================
        //  Underworld / Passage Nodes
        // ================================================================

        /// <summary>Get the design-coordinate position of an underworld passage node.</summary>
        Vector3 UnderworldPassageDesignPosition(int index, int passageCount);

        /// <summary>Get the world-coordinate position of an underworld passage node.</summary>
        Vector3 UnderworldPassagePosition(int index, int passageCount);

        // ================================================================
        //  Meeting Seats
        // ================================================================

        /// <summary>Get the design-coordinate position of a meeting seat.</summary>
        Vector3 MeetingSeatDesignPosition(int seatIndex, int seatCount);

        /// <summary>Get the world-coordinate position of a meeting seat.</summary>
        Vector3 MeetingSeatWorldPosition(int seatIndex, int seatCount);

        // ================================================================
        //  Sabotage Target Locations
        // ================================================================

        /// <summary>Center of the meeting area (design coordinates).</summary>
        Vector2 CurrentMeetingCenter { get; }

        /// <summary>Center of the power room (for blackout sabotage).</summary>
        Vector2 PowerRoomCenter { get; }

        /// <summary>Center of the comms room (for communication jam sabotage).</summary>
        Vector2 CommsRoomCenter { get; }

        /// <summary>Center of the main corridor (for lockdown sabotage).</summary>
        Vector2 MainCorridorCenter { get; }

        // ================================================================
        //  Room Definitions
        // ================================================================

        /// <summary>Get all room/zone definitions for the active map type (design coordinates).</summary>
        OnlineMapService.ShipRoomSpec[] ShipRooms();

        /// <summary>Get Harbour District room definitions.</summary>
        OnlineMapService.ShipRoomSpec[] HarbourDistrictRooms();

        /// <summary>Get Police Station room definitions.</summary>
        OnlineMapService.ShipRoomSpec[] PoliceStationRooms();

        /// <summary>Get Kowloon Walled City room definitions.</summary>
        OnlineMapService.ShipRoomSpec[] KowloonWalledCityRooms();

        // ================================================================
        //  Surveillance Zones
        // ================================================================

        /// <summary>Get all surveillance camera zones for the active map type (design coordinates).</summary>
        OnlineMapService.SurveillanceZoneSpec[] SurveillanceZones();

        /// <summary>Get Harbour District surveillance zones.</summary>
        OnlineMapService.SurveillanceZoneSpec[] HarbourDistrictSurveillanceZones();

        /// <summary>Get Police Station surveillance zones.</summary>
        OnlineMapService.SurveillanceZoneSpec[] PoliceStationSurveillanceZones();

        /// <summary>Get Kowloon Walled City surveillance zones.</summary>
        OnlineMapService.SurveillanceZoneSpec[] KowloonWalledCitySurveillanceZones();
    }
}
