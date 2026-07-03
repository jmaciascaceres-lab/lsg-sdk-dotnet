using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LSG.SDK.Core.Models
{
    public sealed class MechanicDto
    {
        [JsonPropertyName("id_modifiable_mechanic_videogame")]
        public int MmvId { get; set; }

        [JsonPropertyName("id_videogame")]
        public int VideogameId { get; set; }

        [JsonPropertyName("videogame_name")]
        public string VideogameName { get; set; } = string.Empty;

        /// <summary>
        /// Parámetros del efecto. Puede ser null o contener basura tipo
        /// {"additionalProp1": {}} si el catálogo no fue completado correctamente
        /// (caso detectado en algunas mecánicas legacy de Subnautica). El intérprete
        /// de efectos DEBE tolerar esto y aplicar un fallback seguro (ver IEffectInterpreter).
        /// </summary>
        [JsonPropertyName("options")]
        public JsonElement? Options { get; set; }

        [JsonPropertyName("id_modifiable_mechanic")]
        public int MechanicId { get; set; }

        [JsonPropertyName("modifiable_mechanic_name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("modifiable_mechanic_description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("modifiable_mechanic_type")]
        public string Type { get; set; } = string.Empty; // buff | nerf | speed | health | economy | modifier

        /// <summary>
        /// true si Options es null o solo contiene el placeholder de ejemplo de Swagger
        /// ("additionalProp1"). Señal para que el intérprete de efectos loguee un warning
        /// en vez de fallar, y para reportarlo como pendiente de limpieza de catálogo.
        /// </summary>
        public bool HasPlaceholderOrEmptyOptions()
        {
            if (Options is null || Options.Value.ValueKind != JsonValueKind.Object)
                return true;

            var props = Options.Value.EnumerateObject().Select(p => p.Name).ToList();
            return props.Count == 0 || (props.Count == 1 && props[0] == "additionalProp1");
        }
    }

    public sealed class RedeemRequestDto
    {
        [JsonPropertyName("modifiable_mechanic_videogame_id")]
        public int ModifiableMechanicVideogameId { get; set; }

        [JsonPropertyName("point_dimension_id")]
        public int? PointDimensionId { get; set; }

        [JsonPropertyName("attribute_id")]
        public int? AttributeId { get; set; }

        [JsonPropertyName("amount")]
        public int Amount { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }
    }

    public sealed class RedeemPreviewResponse
    {
        [JsonPropertyName("can_redeem")]
        public bool CanRedeem { get; set; }

        [JsonPropertyName("current_balance")]
        public int CurrentBalance { get; set; }

        [JsonPropertyName("required_amount")]
        public int RequiredAmount { get; set; }

        [JsonPropertyName("resulting_balance")]
        public int ResultingBalance { get; set; }

        [JsonPropertyName("point_dimension_id")]
        public int PointDimensionId { get; set; }

        [JsonPropertyName("modifiable_mechanic_videogame_id")]
        public int ModifiableMechanicVideogameId { get; set; }
    }

    public sealed class RedeemResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("points_ledger_id")]
        public int PointsLedgerId { get; set; }

        [JsonPropertyName("redeemed_amount")]
        public int RedeemedAmount { get; set; }

        [JsonPropertyName("resulting_balance")]
        public int ResultingBalance { get; set; }

        [JsonPropertyName("modifiable_mechanic_videogame_id")]
        public int ModifiableMechanicVideogameId { get; set; }
    }

    public sealed class PointsBalanceEntry
    {
        [JsonPropertyName("id_point_dimension")]
        public int PointDimensionId { get; set; }

        [JsonPropertyName("dimension_code")]
        public string DimensionCode { get; set; } = string.Empty; // ej. FISICO_BASE, MENTAL_BASE

        [JsonPropertyName("id_attributes")]
        public int? AttributeId { get; set; }

        [JsonPropertyName("balance")]
        public int Balance { get; set; }
    }
}
