using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace GanglandUndercover.Online
{
    public sealed partial class OnlineMatchController
    {
        private const int LobbyBrowserVisibleRoomLimit = 6;
        private const string LobbySessionTypeValue = "gangland-undercover";
        private const string LobbyPropertyGameType = "game";
        private const string LobbyPropertyRelayCode = "relayCode";
        private const string LobbyPropertyMap = "map";
        private const string LobbyPropertyRules = "rules";
        private const string LocalRelayLobbyRoomId = "local-relay-host";

        private readonly List<LobbyRoomCandidate> lobbyRoomCandidates = new List<LobbyRoomCandidate>();
        private IHostSession publishedLobbySession;
        private ISession joinedLobbySession;
        private bool lobbyBrowserRefreshInProgress;
        private bool lobbyPublishInProgress;
        private bool lobbyJoinInProgress;
        private int lobbyPublishGeneration;
        private int lobbyJoinGeneration;
        private int selectedLobbyRoomIndex = -1;
        private string lobbyBrowserStatus = "Lobby 房间列表待刷新。";
        private string publishedLobbySessionCode = string.Empty;

        public int LobbyRoomCandidateCount => lobbyRoomCandidates.Count;
        public bool LobbyBrowserRefreshInProgress => lobbyBrowserRefreshInProgress;
        public bool LobbyPublishInProgress => lobbyPublishInProgress;
        public bool LobbyJoinInProgress => lobbyJoinInProgress;
        public string LobbyBrowserStatus => lobbyBrowserStatus;
        public string LobbyBrowserSummary => BuildLobbyBrowserSummary(
            lobbyBrowserStatus,
            lobbyBrowserRefreshInProgress,
            lobbyRoomCandidates.Count,
            selectedLobbyRoomIndex);
        public string LobbyRoomListText => BuildLobbyRoomListText(lobbyRoomCandidates);
        public string LobbyBrowserPanelText => LobbyBrowserSummary + "\n" + LobbyRoomListText;

        private void RequestPublishRelayLobbySession()
        {
            if (lobbyPublishInProgress)
            {
                return;
            }

            _ = PublishRelayLobbySessionAsync();
        }

        public void RequestRefreshLobbyRooms()
        {
            if (lobbyBrowserRefreshInProgress)
            {
                return;
            }

            _ = RefreshLobbyRoomsAsync();
        }

        public void RequestJoinSelectedLobbyRoom()
        {
            if (selectedLobbyRoomIndex < 0 || selectedLobbyRoomIndex >= lobbyRoomCandidates.Count)
            {
                lobbyBrowserStatus = "请先刷新并选择一个 Lobby 房间。";
                status = lobbyBrowserStatus;
                return;
            }

            LobbyRoomCandidate room = lobbyRoomCandidates[selectedLobbyRoomIndex];

            if (IsOnline)
            {
                lobbyBrowserStatus = "已在房间内，需先离开当前房间。";
                status = lobbyBrowserStatus;
                return;
            }

            if (string.IsNullOrWhiteSpace(room.RelayCode))
            {
                lobbyBrowserStatus = "该 Lobby 尚未发布 Relay 房间码，暂不能从列表加入。";
                status = lobbyBrowserStatus;
                return;
            }

            LobbyRoomSessionJoin join = BuildLobbyRoomSessionJoin(room.Id, room.RelayCode, room.Id == LocalRelayLobbyRoomId);
            SetRelayJoinInput(join.RelayCode);
            lobbyBrowserStatus = "已选择 Lobby 房间：" + room.Name + "。";

            if (join.CanJoinSession)
            {
                _ = JoinLobbyRoomSessionThenRelayAsync(join, room.Name);
                return;
            }

            status = join.StatusText;
            StartRelayClient();
        }

        private async Task JoinLobbyRoomSessionThenRelayAsync(LobbyRoomSessionJoin join, string roomNameValue)
        {
            if (lobbyJoinInProgress)
            {
                return;
            }

            lobbyJoinInProgress = true;
            int joinGeneration = ++lobbyJoinGeneration;
            lobbyBrowserStatus = "正在加入 Lobby Session：" + LimitText(roomNameValue, 24, "未命名房间") + "。";
            status = lobbyBrowserStatus;

            try
            {
                await CleanupJoinedLobbySessionAsync(false);
                if (joinGeneration != lobbyJoinGeneration)
                {
                    return;
                }

                EnsureServiceBootstrap();
                await serviceBootstrap.InitializeAsync();
                if (joinGeneration != lobbyJoinGeneration)
                {
                    return;
                }

                if (!CanUseSessionBrowser(out string reason))
                {
                    if (joinGeneration == lobbyJoinGeneration)
                    {
                        lobbyBrowserStatus = reason;
                        status = reason;
                    }

                    return;
                }

                ISession joinedSession = await MultiplayerService.Instance.JoinSessionByIdAsync(
                    join.SessionId,
                    BuildLobbyJoinSessionOptions());
                if (joinGeneration != lobbyJoinGeneration)
                {
                    await LeaveLobbySessionAsync(joinedSession);
                    return;
                }

                joinedLobbySession = joinedSession;
                SetRelayJoinInput(join.RelayCode);
                lobbyBrowserStatus = "已加入 Lobby Session，正在连接 Relay。";
                status = lobbyBrowserStatus;
                StartRelayClient();
            }
            catch (Exception exception)
            {
                if (joinGeneration == lobbyJoinGeneration)
                {
                    await CleanupJoinedLobbySessionAsync(false);
                    lobbyBrowserStatus = "Lobby Session 加入失败，仍尝试通过 Relay 房间码加入。";
                    status = lobbyBrowserStatus;
                    SetRelayJoinInput(join.RelayCode);
                    StartRelayClient();
                }

                Debug.LogWarning("Gangland lobby session join skipped: " + exception.Message);
            }
            finally
            {
                if (joinGeneration == lobbyJoinGeneration)
                {
                    lobbyJoinInProgress = false;
                }
            }
        }

        private async Task PublishRelayLobbySessionAsync()
        {
            string safeRelayCode = CleanRelayJoinInput(relayJoinCode);
            if (string.IsNullOrWhiteSpace(safeRelayCode))
            {
                lobbyBrowserStatus = "Relay 房间码未就绪，Lobby 暂不发布。";
                return;
            }

            lobbyPublishInProgress = true;
            int publishGeneration = ++lobbyPublishGeneration;
            lobbyBrowserStatus = BuildLobbyPublishStatus(true, false, string.Empty);

            try
            {
                await CleanupPublishedLobbySessionAsync(false);
                if (publishGeneration != lobbyPublishGeneration)
                {
                    return;
                }

                EnsureServiceBootstrap();
                await serviceBootstrap.InitializeAsync();
                if (publishGeneration != lobbyPublishGeneration)
                {
                    return;
                }

                if (!CanUseSessionBrowser(out string reason))
                {
                    if (publishGeneration == lobbyPublishGeneration)
                    {
                        lobbyBrowserStatus = reason;
                    }

                    return;
                }

                string rules = CurrentLobbyRuleSummary();
                SessionOptions options = BuildRelayLobbySessionOptions(
                    roomName,
                    roomMaxPlayers,
                    safeRelayCode,
                    ActiveMapTypeLobbyLabel(),
                    rules);

                IHostSession createdSession = await MultiplayerService.Instance.CreateSessionAsync(options);
                if (publishGeneration != lobbyPublishGeneration)
                {
                    await DeleteLobbySessionAsync(createdSession);
                    return;
                }

                publishedLobbySession = createdSession;
                publishedLobbySessionCode = CleanRelayJoinInput(publishedLobbySession?.Code);
                lobbyBrowserStatus = BuildLobbyPublishStatus(false, true, publishedLobbySessionCode);
                UpsertLocalRelayLobbyRoom();
            }
            catch (Exception exception)
            {
                if (publishGeneration == lobbyPublishGeneration)
                {
                    IHostSession failedSession = publishedLobbySession;
                    publishedLobbySession = null;
                    publishedLobbySessionCode = string.Empty;
                    _ = DeleteLobbySessionAsync(failedSession);
                    lobbyBrowserStatus = "Lobby 发布失败，仍可分享 Relay 房间码：" + safeRelayCode + "。";
                }

                Debug.LogWarning("Gangland lobby publish skipped: " + exception.Message);
            }
            finally
            {
                if (publishGeneration == lobbyPublishGeneration)
                {
                    lobbyPublishInProgress = false;
                }
            }
        }

        private async Task RefreshLobbyRoomsAsync()
        {
            if (!Application.isPlaying)
            {
                lobbyBrowserStatus = "编辑器未播放，Lobby 房间列表暂不刷新。";
                return;
            }

            lobbyBrowserRefreshInProgress = true;
            lobbyBrowserStatus = "Lobby 正在刷新房间列表。";

            try
            {
                EnsureServiceBootstrap();
                await serviceBootstrap.InitializeAsync();

                if (!CanUseSessionBrowser(out string reason))
                {
                    lobbyRoomCandidates.Clear();
                    selectedLobbyRoomIndex = -1;
                    lobbyBrowserStatus = reason;
                    return;
                }

                QuerySessionsResults results = await MultiplayerService.Instance.QuerySessionsAsync(BuildLobbyQueryOptions());
                lobbyRoomCandidates.Clear();

                if (results != null && results.Sessions != null)
                {
                    int count = Mathf.Min(results.Sessions.Count, LobbyBrowserVisibleRoomLimit);
                    for (int i = 0; i < count; i++)
                    {
                        lobbyRoomCandidates.Add(FromSessionInfo(results.Sessions[i]));
                    }
                }

                selectedLobbyRoomIndex = lobbyRoomCandidates.Count > 0 ? 0 : -1;
                lobbyBrowserStatus = lobbyRoomCandidates.Count == 0
                    ? "Lobby 房间列表为空。"
                    : "Lobby 房间列表已刷新：" + lobbyRoomCandidates.Count + " 间。";
            }
            catch (Exception exception)
            {
                lobbyRoomCandidates.Clear();
                selectedLobbyRoomIndex = -1;
                lobbyBrowserStatus = "Lobby 房间列表刷新失败：" + exception.Message;
                Debug.LogWarning(lobbyBrowserStatus);
            }
            finally
            {
                lobbyBrowserRefreshInProgress = false;
            }
        }

        private bool CanUseSessionBrowser(out string reason)
        {
            if (serviceBootstrap == null)
            {
                reason = "Unity Services 未挂载，Lobby 房间列表暂不可用。";
                return false;
            }

            if (!serviceBootstrap.CloudProjectBound)
            {
                reason = "Cloud Project 未绑定，Lobby 房间列表暂不可用。";
                return false;
            }

            if (!serviceBootstrap.ServicesReady || !serviceBootstrap.AuthenticationReady || !serviceBootstrap.LobbyReady)
            {
                reason = "Lobby 未就绪：" + serviceBootstrap.ServiceReadinessSummary;
                return false;
            }

            if (MultiplayerService.Instance == null)
            {
                reason = "Multiplayer Sessions 未初始化：" + serviceBootstrap.ServiceReadinessSummary;
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private async Task CleanupJoinedLobbySessionAsync(bool invalidateInFlight = true)
        {
            if (invalidateInFlight)
            {
                lobbyJoinGeneration++;
                lobbyJoinInProgress = false;
            }

            ISession session = joinedLobbySession;
            joinedLobbySession = null;

            await LeaveLobbySessionAsync(session);
        }

        private void CleanupJoinedLobbySession()
        {
            _ = CleanupJoinedLobbySessionAsync();
        }

        private static async Task LeaveLobbySessionAsync(ISession session)
        {
            if (session == null)
            {
                return;
            }

            try
            {
                await session.LeaveAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Gangland lobby session leave skipped: " + exception.GetType().Name);
            }
        }

        private async Task CleanupPublishedLobbySessionAsync(bool invalidateInFlight = true)
        {
            if (invalidateInFlight)
            {
                lobbyPublishGeneration++;
                lobbyPublishInProgress = false;
            }

            IHostSession session = publishedLobbySession;
            publishedLobbySession = null;
            publishedLobbySessionCode = string.Empty;

            await DeleteLobbySessionAsync(session);
        }

        private void CleanupPublishedLobbySession()
        {
            _ = CleanupPublishedLobbySessionAsync();
        }

        private static async Task DeleteLobbySessionAsync(IHostSession session)
        {
            if (session == null)
            {
                return;
            }

            try
            {
                await session.DeleteAsync();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Gangland lobby cleanup skipped: " + exception.GetType().Name);
            }
        }

        private static QuerySessionsOptions BuildLobbyQueryOptions()
        {
            QuerySessionsOptions options = new QuerySessionsOptions
            {
                Count = LobbyBrowserVisibleRoomLimit,
                Skip = 0
            };
            options.FilterOptions.Add(new FilterOption(FilterField.StringIndex1, LobbySessionTypeValue, FilterOperation.Equal));
            options.SortOptions.Add(new SortOption(SortOrder.Descending, SortField.LastUpdated));
            return options;
        }

        private static SessionOptions BuildRelayLobbySessionOptions(
            string roomNameValue,
            int maxPlayersValue,
            string relayCodeValue,
            string mapNameValue,
            string ruleSummaryValue)
        {
            return new SessionOptions
            {
                Type = LobbySessionTypeValue,
                Name = LimitText(roomNameValue, 24, "未命名房间"),
                MaxPlayers = Mathf.Max(1, maxPlayersValue),
                IsPrivate = false,
                IsLocked = false,
                SessionProperties = BuildRelayLobbySessionProperties(relayCodeValue, mapNameValue, ruleSummaryValue)
            };
        }

        private static JoinSessionOptions BuildLobbyJoinSessionOptions()
        {
            return new JoinSessionOptions
            {
                Type = LobbySessionTypeValue
            };
        }

        private static Dictionary<string, SessionProperty> BuildRelayLobbySessionProperties(
            string relayCodeValue,
            string mapNameValue,
            string ruleSummaryValue)
        {
            return new Dictionary<string, SessionProperty>
            {
                {
                    LobbyPropertyGameType,
                    new SessionProperty(LobbySessionTypeValue, VisibilityPropertyOptions.Public, PropertyIndex.String1)
                },
                {
                    LobbyPropertyRelayCode,
                    new SessionProperty(CleanRelayJoinInput(relayCodeValue), VisibilityPropertyOptions.Public, PropertyIndex.String2)
                },
                {
                    LobbyPropertyMap,
                    new SessionProperty(LimitText(mapNameValue, 18, "地图待定"), VisibilityPropertyOptions.Public, PropertyIndex.String3)
                },
                {
                    LobbyPropertyRules,
                    new SessionProperty(LimitText(ruleSummaryValue, 28, "默认规则"), VisibilityPropertyOptions.Public)
                }
            };
        }

        private static LobbyRoomCandidate FromSessionInfo(ISessionInfo session)
        {
            string relayCode = GetSessionProperty(session.Properties, LobbyPropertyRelayCode);
            string map = GetSessionProperty(session.Properties, LobbyPropertyMap);
            string rules = GetSessionProperty(session.Properties, LobbyPropertyRules);
            int maxPlayers = Mathf.Max(1, session.MaxPlayers);
            int playerCount = Mathf.Clamp(maxPlayers - session.AvailableSlots, 0, maxPlayers);

            return new LobbyRoomCandidate(
                session.Id,
                session.Name,
                playerCount,
                maxPlayers,
                session.IsLocked,
                session.HasPassword,
                map,
                rules,
                relayCode);
        }

        private void UpsertLocalRelayLobbyRoom()
        {
            string safeRelayCode = CleanRelayJoinInput(relayJoinCode);
            if (string.IsNullOrWhiteSpace(safeRelayCode))
            {
                return;
            }

            int existingIndex = lobbyRoomCandidates.FindIndex(room => room.Id == LocalRelayLobbyRoomId);
            if (existingIndex >= 0)
            {
                lobbyRoomCandidates.RemoveAt(existingIndex);
            }

            int maxPlayers = Mathf.Max(1, roomMaxPlayers);
            int playerCount = Mathf.Clamp(Mathf.Max(1, ConnectedClientCount), 1, maxPlayers);
            string rules = CurrentLobbyRuleSummary();
            lobbyRoomCandidates.Insert(0, new LobbyRoomCandidate(
                LocalRelayLobbyRoomId,
                roomName,
                playerCount,
                maxPlayers,
                false,
                false,
                ActiveMapTypeLobbyLabel(),
                rules,
                safeRelayCode));
            selectedLobbyRoomIndex = 0;

            if (!string.IsNullOrWhiteSpace(publishedLobbySessionCode))
            {
                lobbyBrowserStatus = "Lobby Session 已发布：" + publishedLobbySessionCode + "。";
            }
            else if (!lobbyPublishInProgress)
            {
                lobbyBrowserStatus = "本机 Relay 房间已加入房间列表预览。";
            }
        }

        private string ActiveMapTypeLobbyLabel()
        {
            switch (mapService.ActiveMapType)
            {
                case OnlineMapService.OnlineMapType.PoliceStation:
                    return "警署";
                case OnlineMapService.OnlineMapType.KowloonWalledCity:
                    return "九龙城寨";
                default:
                    return "港区";
            }
        }

        private string CurrentLobbyRuleSummary()
        {
            return roomAutoFillAi ? "AI补位" : "真人优先";
        }

        private static LobbyRoomSessionJoin BuildLobbyRoomSessionJoin(
            string sessionIdValue,
            string relayCodeValue,
            bool allowLocalPreview)
        {
            string safeSessionId = string.IsNullOrWhiteSpace(sessionIdValue) ? string.Empty : sessionIdValue.Trim();
            string safeRelayCode = CleanRelayJoinInput(relayCodeValue);
            bool canJoinSession = !allowLocalPreview
                && !string.IsNullOrWhiteSpace(safeSessionId)
                && !string.IsNullOrWhiteSpace(safeRelayCode);
            string statusText = canJoinSession
                ? "正在通过 Lobby Session 加入 Relay " + safeRelayCode + "。"
                : "正在通过 Relay 房间码加入 " + safeRelayCode + "。";

            return new LobbyRoomSessionJoin(safeSessionId, safeRelayCode, canJoinSession, statusText);
        }

        private static string BuildLobbyPublishStatus(bool publishInProgress, bool published, string sessionCode)
        {
            if (publishInProgress)
            {
                return "Lobby 正在发布 Relay 房间。";
            }

            if (published)
            {
                string safeCode = CleanRelayJoinInput(sessionCode);
                return string.IsNullOrWhiteSpace(safeCode)
                    ? "Lobby Session 已发布。"
                    : "Lobby Session 已发布：" + safeCode + "。";
            }

            return "Lobby 房间列表待刷新。";
        }

        private static string BuildLobbyBrowserSummary(
            string statusValue,
            bool refreshInProgress,
            int visibleRoomCount,
            int selectedIndex)
        {
            string safeStatus = string.IsNullOrWhiteSpace(statusValue)
                ? "Lobby 房间列表待刷新。"
                : statusValue.Trim();
            StringBuilder builder = new StringBuilder();
            builder.Append(safeStatus);
            builder.Append(" | ");
            builder.Append(refreshInProgress ? "正在刷新" : "空闲");
            builder.Append(" | ");
            builder.Append(Mathf.Max(0, visibleRoomCount)).Append(" 间");

            if (visibleRoomCount > 0 && selectedIndex >= 0 && selectedIndex < visibleRoomCount)
            {
                builder.Append(" | 选中第 ").Append(selectedIndex + 1).Append(" 间");
            }
            else if (visibleRoomCount == 0)
            {
                builder.Append(" | 可继续用 Relay 房间码加入");
            }

            return builder.ToString();
        }

        private static string BuildLobbyRoomListText(List<LobbyRoomCandidate> rooms)
        {
            if (rooms == null || rooms.Count == 0)
            {
                return "房间列表: 暂无可显示房间。";
            }

            StringBuilder builder = new StringBuilder();
            int count = Mathf.Min(rooms.Count, LobbyBrowserVisibleRoomLimit);

            for (int i = 0; i < count; i++)
            {
                LobbyRoomCandidate room = rooms[i];
                builder.Append(BuildLobbyRoomLine(
                    i + 1,
                    room.Name,
                    room.PlayerCount,
                    room.MaxPlayers,
                    room.IsLocked,
                    room.HasPassword,
                    room.MapName,
                    room.RuleSummary,
                    room.RelayCode));

                if (i < count - 1)
                {
                    builder.AppendLine();
                }
            }

            return builder.ToString();
        }

        private static string BuildLobbyRoomLine(
            int displayIndex,
            string roomNameValue,
            int playerCountValue,
            int maxPlayersValue,
            bool isLocked,
            bool hasPassword,
            string mapNameValue,
            string ruleSummaryValue,
            string relayCodeValue)
        {
            int maxPlayers = Mathf.Max(1, maxPlayersValue);
            int playerCount = Mathf.Clamp(playerCountValue, 0, maxPlayers);
            string safeName = LimitText(roomNameValue, 24, "未命名房间");
            string safeMap = LimitText(mapNameValue, 18, "地图待定");
            string safeRules = LimitText(ruleSummaryValue, 28, "默认规则");
            string safeRelayCode = CleanRelayJoinInput(relayCodeValue);
            string joinState = RoomJoinState(playerCount, maxPlayers, isLocked, hasPassword, safeRelayCode);

            return Mathf.Max(1, displayIndex)
                + ". " + safeName
                + " | " + playerCount + "/" + maxPlayers
                + " | " + joinState
                + " | " + safeMap
                + " | " + safeRules
                + (string.IsNullOrEmpty(safeRelayCode) ? string.Empty : " | Relay " + safeRelayCode);
        }

        private static string RoomJoinState(int playerCount, int maxPlayers, bool isLocked, bool hasPassword, string relayCode)
        {
            if (isLocked)
            {
                return "锁定";
            }

            if (hasPassword)
            {
                return "密码";
            }

            if (playerCount >= maxPlayers)
            {
                return "已满";
            }

            return string.IsNullOrWhiteSpace(relayCode) ? "待发布 Relay" : "可加入";
        }

        private static string GetSessionProperty(IReadOnlyDictionary<string, SessionProperty> properties, string key)
        {
            if (properties == null || string.IsNullOrEmpty(key))
            {
                return string.Empty;
            }

            return properties.TryGetValue(key, out SessionProperty value) && value != null
                ? value.Value ?? string.Empty
                : string.Empty;
        }

        private readonly struct LobbyRoomSessionJoin
        {
            public readonly string SessionId;
            public readonly string RelayCode;
            public readonly bool CanJoinSession;
            public readonly string StatusText;

            public LobbyRoomSessionJoin(string sessionId, string relayCode, bool canJoinSession, string statusText)
            {
                SessionId = string.IsNullOrWhiteSpace(sessionId) ? string.Empty : sessionId.Trim();
                RelayCode = CleanRelayJoinInput(relayCode);
                CanJoinSession = canJoinSession;
                StatusText = string.IsNullOrWhiteSpace(statusText) ? "正在通过 Relay 房间码加入。" : statusText.Trim();
            }
        }

        private readonly struct LobbyRoomCandidate
        {
            public readonly string Id;
            public readonly string Name;
            public readonly int PlayerCount;
            public readonly int MaxPlayers;
            public readonly bool IsLocked;
            public readonly bool HasPassword;
            public readonly string MapName;
            public readonly string RuleSummary;
            public readonly string RelayCode;

            public LobbyRoomCandidate(
                string id,
                string name,
                int playerCount,
                int maxPlayers,
                bool isLocked,
                bool hasPassword,
                string mapName,
                string ruleSummary,
                string relayCode)
            {
                Id = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
                Name = string.IsNullOrWhiteSpace(name) ? "未命名房间" : name.Trim();
                MaxPlayers = Mathf.Max(1, maxPlayers);
                PlayerCount = Mathf.Clamp(playerCount, 0, MaxPlayers);
                IsLocked = isLocked;
                HasPassword = hasPassword;
                MapName = string.IsNullOrWhiteSpace(mapName) ? "地图待定" : mapName.Trim();
                RuleSummary = string.IsNullOrWhiteSpace(ruleSummary) ? "默认规则" : ruleSummary.Trim();
                RelayCode = CleanRelayJoinInput(relayCode);
            }
        }
    }
}
