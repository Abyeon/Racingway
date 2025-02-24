using FFXIVClientStructs.FFXIV.Client.System.String;
using LiteDB;
using Racingway.Race.Collision.Triggers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Dalamud.Interface.Utility.Raii.ImRaii;
using System.Xml.Linq;

namespace Racingway.Race
{
    public class Address
    {
        [BsonId]
        public ObjectId Id;
        public uint TerritoryId { get; set; }
        public uint MapId { get; set; }
        public string Location { get; set; }

        public Address(uint territoryId, uint mapId, string location)
        {
            Id = ObjectId.NewObjectId();
            TerritoryId = territoryId;
            MapId = mapId;
            Location = location;
        }

        public BsonDocument GetSerialized()
        {
            BsonDocument doc = new BsonDocument();
            doc["_id"] = Id;
            doc["territoryId"] = new BsonValue(TerritoryId);
            doc["mapId"] = new BsonValue(MapId);
            doc["location"] = Location;

            return doc;
        }
    }
}
