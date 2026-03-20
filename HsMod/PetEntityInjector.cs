using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Blizzard.T5.Core;
using HarmonyLib;

namespace HsMod
{
    internal static class PetEntityInjector
    {
        private const int MaxRetry = 300;
        private static bool s_patched;
        private static bool s_injected;
        private static bool s_cardAdded;
        private static bool s_loadCardCalled;
        private static bool s_updatingLayout;
        private static int s_retryCount;
        private static int s_friendlyEntityId = -1;
        private static int s_friendlyControllerId = -1;

        public static int FriendlyPetVariantId = -1;
        public static int OpposingPetVariantId = -1;

        public static void ApplyPatches()
        {
            if (s_patched)
            {
                return;
            }

            new Harmony("com.hsmod.petentityinjector").PatchAll(typeof(PetEntityInjector));
            s_patched = true;
            ResetState();
        }

        private static void ResetState()
        {
            s_injected = false;
            s_cardAdded = false;
            s_loadCardCalled = false;
            s_updatingLayout = false;
            s_retryCount = 0;
            s_friendlyEntityId = -1;
            s_friendlyControllerId = -1;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ZoneCosmetic), "UpdateLayout")]
        private static void ZoneCosmeticUpdateLayoutPrefix(ZoneCosmetic __instance)
        {
            try
            {
                if (FriendlyPetVariantId < 0 && OpposingPetVariantId < 0)
                {
                    return;
                }

                if (s_updatingLayout)
                {
                    return;
                }

                GameState gameState = GameState.Get();
                if (gameState == null)
                {
                    return;
                }

                if (s_injected && s_friendlyEntityId > 0)
                {
                    Map<int, Entity> entityMap = gameState.GetEntityMap();
                    if (entityMap == null || !entityMap.ContainsKey(s_friendlyEntityId))
                    {
                        ResetState();
                    }
                }

                if (!s_injected)
                {
                    InjectPetEntities(gameState);
                    s_injected = true;
                }

                if (!s_cardAdded && FriendlyPetVariantId >= 0 && s_friendlyEntityId > 0)
                {
                    s_retryCount++;
                    if (s_retryCount > MaxRetry)
                    {
                        s_cardAdded = true;
                        LogError("Max retry reached, giving up on pet injection.");
                    }
                    else if (AddEntityCardToZoneCosmetic(gameState, s_friendlyEntityId, FriendlyPetVariantId))
                    {
                        s_cardAdded = true;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(ex.ToString());
            }
        }

        private static void InjectPetEntities(GameState gameState)
        {
            int nextEntityId = GetNextEntityId(gameState);

            if (FriendlyPetVariantId >= 0)
            {
                Player friendlyPlayer = gameState.GetPlayerBySide(Player.Side.FRIENDLY);
                if (friendlyPlayer != null)
                {
                    s_friendlyEntityId = nextEntityId;
                    s_friendlyControllerId = friendlyPlayer.GetPlayerId();
                    InjectOnePet(gameState, nextEntityId++, s_friendlyControllerId, FriendlyPetVariantId);
                }
            }

            if (OpposingPetVariantId >= 0)
            {
                Player opposingPlayer = gameState.GetPlayerBySide(Player.Side.OPPOSING);
                if (opposingPlayer != null)
                {
                    InjectOnePet(gameState, nextEntityId++, opposingPlayer.GetPlayerId(), OpposingPetVariantId);
                }
            }
        }

        private static void InjectOnePet(GameState gameState, int entityId, int controllerId, int variantId)
        {
            try
            {
                string cardId = GetPetCardId(variantId);
                PetVariantDbfRecord variantRecord = GameDbf.PetVariant.GetRecord(variantId);

                Network.HistFullEntity fullEntity = new Network.HistFullEntity();
                Network.Entity networkEntity = new Network.Entity();
                fullEntity.Entity = networkEntity;

                SetNetworkEntityId(networkEntity, entityId);
                SetNetworkEntityCardId(networkEntity, cardId ?? string.Empty);
                InitEntityListProperty(networkEntity, "TagLists");
                InitEntityListProperty(networkEntity, "DefTagLists");
                InitEntityListProperty(networkEntity, "DefTags");

                fullEntity.Entity.Tags = new List<Network.Entity.Tag>
                {
                    MakeTag(49, 9),
                    MakeTag(50, controllerId),
                    MakeTag(202, 45),
                    MakeTag((int)GAME_TAG.PET_VARIANT_ID, variantId)
                };

                if (variantRecord != null)
                {
                    fullEntity.Entity.Tags.Add(MakeTag(4079, variantRecord.PetId));
                }

                gameState.OnRealTimeFullEntity(fullEntity);

                Entity entity = gameState.GetEntity(entityId);
                if (entity == null)
                {
                    entity = CreateRuntimePetEntity(entityId, controllerId, variantId, variantRecord, cardId);
                    if (entity == null || !AddEntityToGameState(gameState, entityId, entity))
                    {
                        LogError($"Injected pet entity {entityId} was not created.");
                        return;
                    }
                }

                entity.SetTag((GAME_TAG)49, 9);
                entity.SetTag((GAME_TAG)50, controllerId);
                entity.SetTag((GAME_TAG)202, 45);
                entity.SetTag(GAME_TAG.PET_VARIANT_ID, variantId);
                if (variantRecord != null)
                {
                    entity.SetTag((GAME_TAG)4079, variantRecord.PetId);
                }
                if (!string.IsNullOrEmpty(cardId))
                {
                    entity.SetCardId(cardId);
                }

                Player player = GetPlayerByControllerId(gameState, controllerId);
                player?.SetPet(entity);
                Log($"Pet injected: entityId={entityId}, variantId={variantId}, controller={controllerId}");
            }
            catch (Exception ex)
            {
                LogError(ex.ToString());
            }
        }

        private static Entity CreateRuntimePetEntity(int entityId, int controllerId, int variantId, PetVariantDbfRecord variantRecord, string cardId)
        {
            try
            {
                Entity entity = (Entity)Activator.CreateInstance(typeof(Entity));

                MethodInfo setEntityIdMethod = typeof(EntityBase).GetMethod("SetEntityId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    ?? typeof(Entity).GetMethod("SetEntityId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (setEntityIdMethod != null)
                {
                    setEntityIdMethod.Invoke(entity, new object[] { entityId });
                }
                else
                {
                    FieldInfo entityIdField = typeof(EntityBase).GetField("m_id", BindingFlags.Instance | BindingFlags.NonPublic)
                        ?? typeof(EntityBase).GetField("m_entityId", BindingFlags.Instance | BindingFlags.NonPublic);
                    entityIdField?.SetValue(entity, entityId);
                }

                entity.SetTag((GAME_TAG)49, 9);
                entity.SetTag((GAME_TAG)50, controllerId);
                entity.SetTag((GAME_TAG)202, 45);
                entity.SetTag(GAME_TAG.PET_VARIANT_ID, variantId);
                if (variantRecord != null)
                {
                    entity.SetTag((GAME_TAG)4079, variantRecord.PetId);
                }
                if (!string.IsNullOrEmpty(cardId))
                {
                    entity.SetCardId(cardId);
                }

                return entity;
            }
            catch (Exception ex)
            {
                LogError(ex.ToString());
                return null;
            }
        }

        private static bool AddEntityToGameState(GameState gameState, int entityId, Entity entity)
        {
            try
            {
                FieldInfo entityMapField = typeof(GameState).GetField("m_entityMap", BindingFlags.Instance | BindingFlags.NonPublic);
                object entityMap = entityMapField?.GetValue(gameState);
                if (entityMap == null)
                {
                    return false;
                }

                MethodInfo containsKeyMethod = entityMap.GetType().GetMethod("ContainsKey");
                bool contains = (bool?)containsKeyMethod?.Invoke(entityMap, new object[] { entityId }) ?? false;
                if (contains)
                {
                    return true;
                }

                MethodInfo addMethod = entityMap.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(int), typeof(Entity) }, null);
                if (addMethod == null)
                {
                    LogError("Cannot find Add for GameState entity map.");
                    return false;
                }

                addMethod.Invoke(entityMap, new object[] { entityId, entity });
                return true;
            }
            catch (Exception ex)
            {
                LogError(ex.ToString());
                return false;
            }
        }

        private static bool AddEntityCardToZoneCosmetic(GameState gameState, int entityId, int variantId)
        {
            try
            {
                Entity entity = gameState.GetEntity(entityId);
                if (entity == null)
                {
                    return false;
                }

                if (!s_loadCardCalled)
                {
                    s_loadCardCalled = true;

                    string cardId = GetPetCardId(variantId);
                    if (string.IsNullOrEmpty(cardId))
                    {
                        LogError("CardId is empty, cannot load card.");
                        return false;
                    }

                    entity.SetCardId(cardId);
                    typeof(Entity).GetMethod("InitCard", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(entity, null);

                    ZoneMgr zoneMgr = ZoneMgr.Get();
                    ZoneCosmetic zoneCosmetic = zoneMgr?.FindZoneOfType<ZoneCosmetic>(Player.Side.FRIENDLY);
                    if (zoneCosmetic == null)
                    {
                        return false;
                    }

                    Card card = entity.GetCard();
                    if (card == null)
                    {
                        return false;
                    }

                    typeof(Card).GetField("m_zone", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(card, zoneCosmetic);

                    Type loadCardDataType = typeof(Entity).GetNestedType("LoadCardData", BindingFlags.Public | BindingFlags.NonPublic);
                    object loadCardData = Activator.CreateInstance(loadCardDataType);
                    loadCardDataType.GetField("updateActor")?.SetValue(loadCardData, true);
                    loadCardDataType.GetField("restartStateSpells")?.SetValue(loadCardData, false);
                    loadCardDataType.GetField("fromChangeEntity")?.SetValue(loadCardData, false);

                    MethodInfo loadCardMethod = typeof(Entity).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(m => m.Name == "LoadCard" && m.GetParameters().Length == 3);
                    loadCardMethod?.Invoke(entity, new object[] { cardId, loadCardData, false });

                    List<Card> cards = typeof(Zone).GetField("m_cards", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(zoneCosmetic) as List<Card>;
                    if (cards != null && !cards.Contains(card))
                    {
                        cards.Add(card);
                    }
                }

                Card petCard = entity.GetCard();
                if (petCard == null)
                {
                    return false;
                }

                Actor actor = petCard.GetActor();
                if (actor == null)
                {
                    return false;
                }

                if (actor.m_petController == null)
                {
                    PetControllerGame petController = actor.GetComponentInChildren<PetControllerGame>(true);
                    if (petController != null)
                    {
                        actor.m_petController = petController;
                    }
                }

                ZoneCosmetic friendlyZone = ZoneMgr.Get()?.FindZoneOfType<ZoneCosmetic>(Player.Side.FRIENDLY);
                if (friendlyZone == null)
                {
                    return false;
                }

                s_updatingLayout = true;
                friendlyZone.UpdateLayout();
                s_updatingLayout = false;

                // The first SetPet call often happens before layout is ready and gets blocked.
                // Trigger the native update path again after layout so the pet model is created.
                actor.UpdatePetComponents();
                actor.m_petController?.CreatePetObject();

                Log("Pet card added to ZoneCosmetic successfully.");
                return true;
            }
            catch (Exception ex)
            {
                s_updatingLayout = false;
                LogError(ex.ToString());
                return false;
            }
        }

        private static Player GetPlayerByControllerId(GameState gameState, int controllerId)
        {
            Player friendlyPlayer = gameState.GetPlayerBySide(Player.Side.FRIENDLY);
            if (friendlyPlayer != null && friendlyPlayer.GetPlayerId() == controllerId)
            {
                return friendlyPlayer;
            }

            Player opposingPlayer = gameState.GetPlayerBySide(Player.Side.OPPOSING);
            if (opposingPlayer != null && opposingPlayer.GetPlayerId() == controllerId)
            {
                return opposingPlayer;
            }

            return null;
        }

        private static string GetPetCardId(int variantId)
        {
            try
            {
                PetsManager petsManager = PetsManager.Get();
                if (petsManager != null)
                {
                    int cardDbId;
                    if (petsManager.TryGetCardIdFromPetVariantId(variantId, out cardDbId))
                    {
                        return GameUtils.TranslateDbIdToCardId(cardDbId, false) ?? string.Empty;
                    }
                }

                PetVariantDbfRecord record = GameDbf.PetVariant.GetRecord(variantId);
                if (record != null)
                {
                    return GameUtils.TranslateDbIdToCardId(record.CardId, false) ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                LogError(ex.ToString());
            }

            return string.Empty;
        }

        private static void InitEntityListProperty(Network.Entity entity, string propName)
        {
            PropertyInfo property = typeof(Network.Entity).GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite)
            {
                property.SetValue(entity, Activator.CreateInstance(property.PropertyType));
                return;
            }

            FieldInfo field = typeof(Network.Entity).GetField("<" + propName + ">k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(entity, Activator.CreateInstance(field.FieldType));
            }
        }

        private static void SetNetworkEntityId(Network.Entity entity, int id)
        {
            foreach (string name in new[] { "Id", "ID", "m_id", "EntityId", "id" })
            {
                FieldInfo field = typeof(Network.Entity).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(int))
                {
                    field.SetValue(entity, id);
                    return;
                }

                PropertyInfo property = typeof(Network.Entity).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.CanWrite && property.PropertyType == typeof(int))
                {
                    property.SetValue(entity, id);
                    return;
                }
            }
        }

        private static void SetNetworkEntityCardId(Network.Entity entity, string cardId)
        {
            foreach (string name in new[] { "CardId", "CardID", "m_cardId", "m_cardID", "cardId" })
            {
                FieldInfo field = typeof(Network.Entity).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field != null && field.FieldType == typeof(string))
                {
                    field.SetValue(entity, cardId);
                    return;
                }

                PropertyInfo property = typeof(Network.Entity).GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.CanWrite && property.PropertyType == typeof(string))
                {
                    property.SetValue(entity, cardId);
                    return;
                }
            }
        }

        private static Network.Entity.Tag MakeTag(int tag, int value)
        {
            return new Network.Entity.Tag
            {
                Name = tag,
                Value = value
            };
        }

        private static int GetNextEntityId(GameState gameState)
        {
            for (int entityId = 9000; entityId < 9100; entityId++)
            {
                if (gameState.GetEntity(entityId) == null)
                {
                    return entityId;
                }
            }

            return 9000;
        }

        private static void Log(string message)
        {
            Utils.MyLogger(BepInEx.Logging.LogLevel.Info, "[PetInjector] " + message);
        }

        private static void LogError(string message)
        {
            Utils.MyLogger(BepInEx.Logging.LogLevel.Error, "[PetInjector] " + message);
        }
    }
}
