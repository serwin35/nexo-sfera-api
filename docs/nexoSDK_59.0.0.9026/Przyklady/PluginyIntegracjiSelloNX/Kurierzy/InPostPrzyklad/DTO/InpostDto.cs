using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

// Zestaw klas utworzonych ręcznie lub za pomocą narzędzi typu swagger, zawierającach obiekty wysyłane i odbierane z serwisu kurierskiego
// Klasy te są serializowane (i deserializowane ) do odpowiedniego formatu (np. JSON).
namespace InPostPrzyklad.DTO
{
    internal class InpostActiveShipmentsDto
    {
        [JsonProperty("items")]
        public List<InpostShipmentDto> Shipments { get; set; }
    }

    internal class InpostAttributesDto
    {
        [JsonProperty("sending_method")]
        public string SendingMethod { get; set; }
        [JsonProperty("target_point")]
        public string TargetPoint { get; set; }
        [JsonProperty("dropoff_point")]
        public string DropoffPoint { get; set; }
        [JsonProperty("allegro_transaction_id")]
        public string AllegroTransactionId { get; set; }
        [JsonProperty("allegro_user_id")]
        public string AllegroUserId { get; set; }
        [JsonProperty("dispatch_order_id")]
        public string DispatchOrderId { get; set; }
    }

    internal class InpostCreateShipmentDto
    {
        [JsonProperty("receiver")]
        public InpostPeerDto Receiver { get; set; }
        [JsonProperty("sender")]
        public InpostPeerDto Sender { get; set; }
        [JsonProperty("custom_attributes")]
        public InpostAttributesDto CustomAttributes { get; set; }
        [JsonProperty("parcels")]
        public InpostParcelDto[] Parcels { get; set; }
        [JsonProperty("service")]
        public string Service { get; set; }
        [JsonProperty("additional_services")]
        public List<string> AdditionalServices { get; set; }
        [JsonProperty("comments")]
        public string Comments { get; set; }
        [JsonProperty("cod")]
        public InpostMoneyDto Cod { get; set; }
        [JsonProperty("insurance")]
        public InpostMoneyDto Insurance { get; set; }
        [JsonProperty("reference")]
        public string Reference { get; set; }
        [JsonProperty("external_customer_id")]
        public string ExternalCustomerId { get; set; }
        [JsonProperty("mpk")]
        public string Mpk { get; set; }
    }

    internal class InpostMoneyDto
    {
        [JsonProperty("currency")]
        public string Currency { get; set; }
        [JsonProperty("amount")]
        public decimal? Amount { get; set; }
    }

    internal class InpostOfferDto
    {
        public int Id { get; set; }
        public InpostServiceDto Service { get; set; }
        public InpostServiceDto Carrier { get; set; }
        public List<string> AdditionalServices { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime ExpireTime { get; set; }
        public decimal? Rate { get; set; }
        public string Currency { get; set; }
    }

    internal class InpostParcelDto
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("template")]
        public string Template { get; set; }
        [JsonProperty("tracking_number")]
        public string TrackingNumber { get; set; }
        [JsonProperty("is_non_standard")]
        public bool IsNonStandard { get; set; }
        [JsonProperty("weight")]
        public InpostWeightDto Weight { get; set; }
        [JsonProperty("dimensions")]
        public InpostDimensionDto Dimensions { get; set; }

    }
    internal class InpostDimensionDto
    {
        [JsonProperty("length")]
        public decimal Length { get; set; }
        [JsonProperty("width")]
        public decimal Width { get; set; }
        [JsonProperty("height")]
        public decimal Height { get; set; }
        [JsonProperty("unit")]
        public string Unit { get; set; }
    }
    internal class InpostWeightDto
    {
        [JsonProperty("amount")]
        public decimal Amount { get; set; }
        [JsonProperty("unit")]
        public string Unit { get; set; }
    }

    internal class InpostPeerDto
    {
        [JsonProperty("first_name")]
        public string FirstName { get; set; }
        [JsonProperty("last_name")]
        public string LastName { get; set; }
        [JsonProperty("company_name")]
        public string CompanyName { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("email")]
        public string Email { get; set; }
        [JsonProperty("phone")]
        public string Phone { get; set; }
        [JsonProperty("address")]
        public InpostAddressDto Address { get; set; }
    }

    internal class InpostServiceDto
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("description")]
        public string Description { get; set; }
    }

    internal class InpostDispatchShipmentDto
    {
        [JsonProperty("href")]
        public string Href { get; set; }
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("tracking_number")]
        public string TrackingNumber { get; set; }
    }

    internal class InpostShipmentDto
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("receiver")]
        public InpostPeerDto Receiver { get; set; }
        [JsonProperty("parcels")]
        public InpostParcelDto[] Parcel { get; set; }
        [JsonProperty("status")]
        public string Status { get; set; }
        [JsonProperty("selected_offer")]
        public InpostOfferDto Offer { get; set; }
        [JsonProperty("offers")]
        public List<InpostOfferDto> AvailableOffers { get; set; }
        [JsonProperty("transactions")]
        public List<InpostTransactionDto> Transactions { get; set; }
        [JsonProperty("cod")]
        public InpostMoneyDto Cod { get; set; }
        [JsonProperty("insurance")]
        public InpostMoneyDto Insurance { get; set; }
        [JsonProperty("custom_attributes")]
        public InpostAttributesDto Attributes { get; set; }
        [JsonProperty("created_at")]
        public DateTime CreationTime { get; set; }
        [JsonProperty("created_by_id")]
        public int? CreatorId { get; set; }
        [JsonProperty("sender")]
        public InpostPeerDto Sender { get; set; }
        [JsonProperty("reference")]
        public string Reference { get; set; }
        [JsonProperty("is_return")]
        public bool IsReturn { get; set; }
        [JsonProperty("tracking_number")]
        public string TrackingNumber { get; set; }
        [JsonProperty("external_customer_id")]
        public string ExternalCustomerId { get; set; }
        [JsonProperty("service")]
        public string Service { get; set; }
        [JsonProperty("additional_services")]
        public string[] AdditionalServices { get; set; }
        [JsonProperty("comments")]
        public string Comments { get; set; } = default;
        [JsonProperty("unavailability_reasons")]
        public UnavailableError[] UnavailableReasons { get; set; }
    }

    internal class InpostTransactionDto
    {
        public int Id { get; set; }
        public string Status { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime UpodatedTime { get; set; }
        public int OfferId { get; set; }
    }

    public class InpostAddressDto
    {
        [JsonProperty("id")]
        public string Id { get; set; }
        [JsonProperty("street")]
        public string Street { get; set; }
        [JsonProperty("city")]
        public string City { get; set; }
        [JsonProperty("building_number")]
        public string BuildingNumber { get; set; }
        [JsonProperty("post_code")]
        public string ZipCode { get; set; }
        [JsonProperty("country_code")]
        public string CountryCode { get; set; }
    }

    public class InpostCommentDto
    {
        [JsonProperty("id")]
        public long Id { get; set; }
        [JsonProperty("comment")]
        public string Comment { get; set; }
        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    internal class InpostDispatchOrderRequest
    {
        [JsonProperty("shipments")]
        public List<string> Shipments { get; set; }

        [JsonProperty("comment")]
        public string Comment { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("phone")]
        public string Phone { get; set; }

        [JsonProperty("email")]
        public string Email { get; set; }

        [JsonProperty("address")]
        public InpostAddressDto Address { get; set; }
    }

    internal class InpostDispatchOrderResponseDto
    {
        [JsonProperty("href")]
        public string Href { get; set; }
        [JsonProperty("id")]
        public long Id { get; set; }
        [JsonProperty("status")]
        public InpostDispatchOrderStatusDto Status { get; set; }
        [JsonProperty("external_id")]
        public long ExternalId { get; set; }
        [JsonProperty("price")]
        public decimal? Price { get; set; }
        [JsonProperty("address")]
        public InpostAddressDto Address { get; set; }
        [JsonProperty("statuses")]
        public List<string> Statuses { get; set; }
        [JsonProperty("shipments")]
        public List<InpostShipmentDto> Shipments { get; set; }
        [JsonProperty("comments")]
        public List<InpostCommentDto> Comments { get; set; }
        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }
        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    internal class InpostCreateDispatchOrderResponseDto
    {
        [JsonProperty("href")]
        public string Href { get; set; }
        [JsonProperty("id")]
        public long Id { get; set; }
        [JsonProperty("status")]
        public InpostDispatchOrderStatusDto Status { get; set; }
        [JsonProperty("address")]
        public InpostAddressDto Address { get; set; }
        [JsonProperty("shipments")]
        public List<InpostDispatchShipmentDto> Shipments { get; set; }
        [JsonProperty("comments")]
        public List<InpostCommentDto> Comments { get; set; }
        [JsonProperty("created_at")]
        public DateTime CreatedAt { get; set; }
        [JsonProperty("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [JsonConverter(typeof(StringEnumConverter))]
    internal enum InpostDispatchOrderStatusDto
    {
        [EnumMember(Value = "sent")]
        Sent = 1,
        [EnumMember(Value = "new")]
        New = 2
    }

    public class UnavailableError
    {
        public UnavailableError()
        {

        }
        public UnavailableError(string key, string msg)
        {
            Key = key;
            Msg = msg;
        }
        /// <summary>
        ///     Key
        /// </summary>
        [JsonProperty("key")]
        public string Key { get; set; }
        /// <summary>
        ///     Msg
        /// </summary>
        [JsonProperty("msg")]
        public string Msg { get; set; }
    }

    public class InpostBadRequestDto
    {
        [JsonProperty("error")]
        public string Error { get; set; }
        [JsonProperty("details")]
        public object Errors { get; set; }
    }
}
