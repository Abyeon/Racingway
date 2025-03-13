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
using Racingway.Utils;

namespace Racingway.Race
{
    public class Address
    {
        [BsonId]
        private ObjectId Id { get; set; }
        public uint TerritoryId { get; set; }
        public uint MapId { get; set; }
        public string LocationId { get; set; }
        public string ReadableName { get; set; }

        public Address(uint territoryId, uint mapId, string location, string readableName)
        {
            Id = ObjectId.NewObjectId();
            TerritoryId = territoryId;
            MapId = mapId;
            LocationId = location;
            ReadableName = readableName;
        }
        public Address(ObjectId id, uint territoryId, uint mapId, string location, string readableName)
        {
            Id = id;
            TerritoryId = territoryId;
            MapId = mapId;
            LocationId = location;
            ReadableName = readableName;
        }


        public BsonDocument GetSerialized()
        {
            BsonDocument doc = new BsonDocument();
            doc["_id"] = Id;
            doc["territoryId"] = TerritoryId.ToString();
            doc["mapId"] = MapId.ToString();
            doc["locationId"] = LocationId;
            doc["readableName"] = ReadableName;

            return doc;
        }
    }
}
