using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LSG.SDK.Core.Models
{
    public sealed class MechanicDto
    {
        [JsonProperty("id_modifiable_mechanic_videogame")]
        public int MmvId { get; set; }

        [JsonProperty("id_videogame")]
        public int VideogameId { get; set; }

        [JsonProperty("videogame_name")]
        public string VideogameName { get; set; } = string.Empty;

        /// <summary>
        /// Parámetros del efecto. Puede ser null o contener basura tipo
        /// {"additionalProp1": {}} si el catálogo no fue completado correctamente
        /// (caso detectado en algunas mecánicas legacy de Subnautica). El intérprete
        /// de efectos DEBE tolerar esto y aplicar un fallback seguro (ver IEffectInterpreter).
        /// </summary>
        [JsonProperty("options")]
        public JToken? Options { get; set; }

        [JsonProperty("id_modifiable_mechanic")]
        public int MechanicId { get; set; }

        [JsonProperty("modifiable_mechanic_name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("modifiable_mechanic_description")]
        public string Description { get; set; } = string.Empty;

        [JsonProperty("modifiable_mechanic_type")]
        public string Type { get; set; } = string.Empty; // buff | nerf | speed | health | economy | modifier

        /// <summary>
        /// true si Options es null o solo contiene el placeholder de ejemplo de Swagger
        /// ("additionalProp1"). Señal para que el intérprete de efectos loguee un warning
        /// en vez de fallar, y para reportarlo como pendiente de limpieza de catálogo.
        /// </summary>
        public bool HasPlaceholderOrEmptyOptions()
        {
            if (Options is not JObject obj)
                return true;

            var props = obj.Properties().Select(p => p.Name).ToList();
            return props.Count == 0 || (props.Count == 1 && props[0] == "additionalProp1");
        }
    }

    public sealed class RedeemRequestDto
    {
        [JsonProperty("modifiable_mechanic_videogame_id")]
        public int ModifiableMechanicVideogameId { get; set; }

        [JsonProperty("point_dimension_id")]
        public int? PointDimensionId { get; set; }

        [JsonProperty("attribute_id")]
        public int? AttributeId { get; set; }

        [JsonProperty("amount")]
        public int Amount { get; set; }

        [JsonProperty("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public sealed class RedeemPreviewResponse
    {
        [JsonProperty("can_redeem")]
        public bool CanRedeem { get; set; }

        [JsonProperty("current_balance")]
        public int CurrentBalance { get; set; }

        [JsonProperty("required_amount")]
        public int RequiredAmount { get; set; }

        [JsonProperty("resulting_balance")]
        public int ResultingBalance { get; set; }

        [JsonProperty("point_dimension_id")]
        public int PointDimensionId { get; set; }

        [JsonProperty("modifiable_mechanic_videogame_id")]
        public int ModifiableMechanicVideogameId { get; set; }
    }

    public sealed class RedeemResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; } = string.Empty;

        [JsonProperty("points_ledger_id")]
        public int PointsLedgerId { get; set; }

        [JsonProperty("redeemed_amount")]
        public int RedeemedAmount { get; set; }

        [JsonProperty("resulting_balance")]
        public int ResultingBalance { get; set; }

        [JsonProperty("modifiable_mechanic_videogame_id")]
        public int ModifiableMechanicVideogameId { get; set; }
    }

    public sealed class PointsBalanceEntry
    {
        [JsonProperty("id_point_dimension")]
        public int PointDimensionId { get; set; }

        [JsonProperty("dimension_code")]
        public string DimensionCode { get; set; } = string.Empty; // ej. FISICO_BASE, MENTAL_BASE

        [JsonProperty("id_attributes")]
        public int? AttributeId { get; set; }

        [JsonProperty("balance")]
        public int Balance { get; set; }
    }
}
