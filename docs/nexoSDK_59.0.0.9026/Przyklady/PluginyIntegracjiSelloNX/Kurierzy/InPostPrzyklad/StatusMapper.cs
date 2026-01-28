using InsERT.Moria.HandelElektroniczny.Rozszerzenia.Kurierzy;

namespace InPostPrzyklad
{
    /// <summary>
    /// Przykładowa klasa ułatwiająca mapowanie statusów kurierskich na statusy Sello NX oraz zwracająca zwięzłe nazwy tych statusów, 
    /// które Sello NX prezentuje użytkownikowi programu.
    /// </summary>
    public static class StatusMapper
    {
        public const string InPostCreated = "created";
        public const string InPostOffers_prepared = "offers_prepared";
        public const string InPostOffer_selected = "offer_selected";
        public const string InPostConfirmed = "confirmed";
        public const string InPostDispatched_by_sender = "dispatched_by_sender";
        public const string InPostCollected_from_sender = "collected_from_sender";
        public const string InPostTaken_by_courier = "taken_by_courier";
        public const string InPostAdopted_at_source_branch = "adopted_at_source_branch";
        public const string InPostSent_from_source_branch = "sent_from_source_branch";
        public const string InPostReady_to_pickup_from_pok = "ready_to_pickup_from_pok";
        public const string InPostReady_to_pickup_from_pok_registered = "ready_to_pickup_from_pok_registered";
        public const string InPostOversized = "oversized";
        public const string InPostAdopted_at_sorting_center = "adopted_at_sorting_center";
        public const string InPostSent_from_sorting_center = "sent_from_sorting_center";
        public const string InPostAdopted_at_target_branch = "adopted_at_target_branch";
        public const string InPostOut_for_delivery = "out_for_delivery";
        public const string InPostReady_to_pickup = "ready_to_pickup";
        public const string InPostPickup_reminder_sent = "pickup_reminder_sent";
        public const string InPostDelivered = "delivered";
        public const string InPostPickup_time_expired = "pickup_time_expired";
        public const string InPostAvizo = "avizo";
        public const string InPostClaimed = "claimed";
        public const string InPostReturned_to_sender = "returned_to_sender";
        public const string InPostCanceled = "canceled";
        public const string InPostOther = "other";
        public const string InPostDispatched_by_sender_to_pok = "dispatched_by_sender_to_pok";
        public const string InPostOut_for_delivery_to_address = "out_for_delivery_to_address";
        public const string InPostPickup_reminder_sent_address = "pickup_reminder_sent_address";
        public const string InPostRejected_by_receiver = "rejected_by_receiver";
        public const string InPostUndelivered_wrong_address = "undelivered_wrong_address";
        public const string InPostUndelivered_incomplete_address = "undelivered_incomplete_address";
        public const string InPostUndelivered_unknown_receiver = "undelivered_unknown_receiver";
        public const string InPostUndelivered_cod_cash_receiver = "undelivered_cod_cash_receiver";
        public const string InPostTaken_by_courier_from_pok = "taken_by_courier_from_pok";
        public const string InPostUndelivered = "undelivered";
        public const string InPostReturn_pickup_confirmation_to_sender = "return_pickup_confirmation_to_sender";
        public const string InPostReady_to_pickup_from_branch = "ready_to_pickup_from_branch";
        public const string InPostDelay_in_delivery = "delay_in_delivery";
        public const string InPostRedirect_to_box = "redirect_to_box";
        public const string InPostCanceled_redirect_to_box = "canceled_redirect_to_box";
        public const string InPostReaddressed = "readdressed";
        public const string InPostUndelivered_no_mailbox = "undelivered_no_mailbox";
        public const string InPostUndelivered_not_live_address = "undelivered_not_live_address";
        public const string InPostUndelivered_lack_of_access_letterbox = "undelivered_lack_of_access_letterbox";
        public const string InPostMissing = "missing";
        public const string InPostStack_in_customer_service_point = "stack_in_customer_service_point";
        public const string InPostStack_parcel_pickup_time_expired = "stack_parcel_pickup_time_expired";
        public const string InPostUnstack_from_customer_service_point = "unstack_from_customer_service_point";
        public const string InPostCourier_avizo_in_customer_service_point = "courier_avizo_in_customer_service_point";
        public const string InPostTaken_by_courier_from_customer_service_point = "taken_by_courier_from_customer_service_point";
        public const string InPostStack_in_box_machine = "stack_in_box_machine";
        public const string InPostUnstack_from_box_machine = "unstack_from_box_machine";
        public const string InPostStack_parcel_in_box_machine_pickup_time_expired = "stack_parcel_in_box_machine_pickup_time_expired";

        /// <summary>
        /// Metoda zwracająca zwięzły (w miarę możliwości) tekst opisujący każdy status wysyłki, który może się pojawić u kuriera.
        /// https://dokumentacja-inpost.atlassian.net/wiki/spaces/PL/pages/11731056/Statusy
        /// </summary>
        /// <param name="status">Status u kuriera (najczęściej) w postaci indetyfikatora tekstowego</param>
        /// <returns>Krótka nazwa opisująca status, prezentowana użytkownikowi w programie</returns>
        public static string GetStatusDescription(string status)
        {
            switch (status)
            {
                case InPostCreated:
                    return "Przesyłka utworzona.";
                case InPostOffers_prepared:
                    return "Przygotowano oferty.";
                case InPostOffer_selected:
                    return "Oferta wybrana.";
                case InPostConfirmed:
                    return "Przygotowana przez Nadawcę.";
                case InPostDispatched_by_sender:
                    return "Paczka nadana w paczkomacie.";
                case InPostCollected_from_sender:
                    return "Odebrana od klienta.";
                case InPostTaken_by_courier:
                    return "Odebrana od Nadawcy.";
                case InPostAdopted_at_source_branch:
                    return "Przyjęta w oddziale InPost.";
                case InPostSent_from_source_branch:
                    return "W trasie.";
                case InPostReady_to_pickup_from_pok:
                    return "Czeka na odbiór w Punkcie Obsługi Klienta - 14 dni.";
                case InPostReady_to_pickup_from_pok_registered:
                    return "Czeka na odbiór w Punkcie Obsługi Klienta - 3 dni";
                case InPostOversized:
                    return "Przesyłka ponadgabarytowa.";
                case InPostAdopted_at_sorting_center:
                    return "Przyjęta w Sortowni.";
                case InPostSent_from_sorting_center:
                    return "Wysłana z Sortowni.";
                case InPostAdopted_at_target_branch:
                    return "Przyjęta w Oddziale Docelowym.";
                case InPostOut_for_delivery:
                    return "Przekazano do doręczenia.";
                case InPostReady_to_pickup:
                    return "Umieszczona w Paczkomacie (odbiorczym).";
                case InPostPickup_reminder_sent:
                    return "Przypomnienie o czekającej paczce.";
                case InPostDelivered:
                    return "Dostarczona.";
                case InPostPickup_time_expired:
                    return "Upłynął termin odbioru.";
                case InPostAvizo:
                    return "Powrót do oddziału.";
                case InPostClaimed:
                    return "Zareklamowana w paczkomacie.";
                case InPostReturned_to_sender:
                    return "Zwrot do nadawcy.";
                case InPostCanceled:
                    return "Anulowano etykietę.";
                case InPostOther:
                    return "Inny status.";
                case InPostDispatched_by_sender_to_pok:
                    return "Nadana w Punkcie Obsługi Klienta.";
                case InPostOut_for_delivery_to_address:
                    return "W doręczeniu.";
                case InPostPickup_reminder_sent_address:
                    return "W doręczeniu - klient nieobecny.";
                case InPostRejected_by_receiver:
                    return "Odmowa przyjęcia.";
                case InPostUndelivered_wrong_address:
                    return "Brak możliwości doręczenia - błędne dane adresowe.";
                case InPostUndelivered_incomplete_address:
                    return "Brak możliwości doręczenia - niepełne dane adresowe.";
                case InPostUndelivered_unknown_receiver:
                    return "Brak możliwości doręczenia - Odbiorca nieznany.";
                case InPostUndelivered_cod_cash_receiver:
                    return "Brak możliwości doręczenia - Odbiorca nie miał gotówki.";
                case InPostTaken_by_courier_from_pok:
                    return "W drodze do oddziału nadawczego InPost.";
                case InPostUndelivered:
                    return "Przekazanie do magazynu przesyłek niedoręczalnych.";
                case InPostReturn_pickup_confirmation_to_sender:
                    return "Przygotowano dokumenty zwrotne.";
                case InPostReady_to_pickup_from_branch:
                    return "Paczka nieodebrana – czeka w Oddziale.";
                case InPostDelay_in_delivery:
                    return "Możliwe opóźnienie doręczenia.";
                case InPostRedirect_to_box:
                    return "Przekierowano do Paczkomatu";
                case InPostCanceled_redirect_to_box:
                    return "Anulowano przekierowanie.";
                case InPostReaddressed:
                    return "Przekierowano na inny adres.";
                case InPostUndelivered_no_mailbox:
                    return "Brak możliwości doręczenia - brak skrzynki pocztowej.";
                case InPostUndelivered_not_live_address:
                    return "Brak możliwości doręczenia - Odbiorca nie mieszka pod wskazanym adresem.";
                case InPostUndelivered_lack_of_access_letterbox:
                    return "Brak możliwości doręczenia - zwrot do Nadawcy.";
                case InPostMissing:
                    return "Translation missing: pl_PL.statuses.missing.title";
                case InPostStack_in_customer_service_point:
                    return "Paczka magazynowana w POP.";
                case InPostStack_parcel_pickup_time_expired:
                    return "Upłynął termin odbioru paczki magazynowanej.";
                case InPostUnstack_from_customer_service_point:
                    return "W drodze do wybranego paczkomatu.";
                case InPostCourier_avizo_in_customer_service_point:
                    return "Oczekuje na odbiór.";
                case InPostTaken_by_courier_from_customer_service_point:
                    return "Zwrócona do nadawcy.";
                case InPostStack_in_box_machine:
                    return "Paczka magazynowana w Paczkomacie tymczasowym.";
                case InPostUnstack_from_box_machine:
                    return "Paczka w drodze do pierwotnie wybranego Paczkomatu.";
                case InPostStack_parcel_in_box_machine_pickup_time_expired:
                    return "Upłynął termin odbioru paczki magazynowanej.";
                default:
                    // W przypadku gdy kuriera dodał nowy status, zwracamy jego identyfikator, dzięki czemu użytkownik zobaczy choć identyfikator tego statusu
                    return status;
            }

        }

        /// <summary>
        /// Metoda mapująca status kuriera na status w Sello NX.
        /// StatusPaczki jest używany w Sello NX do różnych celów, m.in. dzięki temu program jest w stanie odpowiednio zarządzać 
        /// funkcjami w programie, aby interfejs programu był w miarę jednolity dla różnych kurierów
        /// </summary>
        /// <param name="status">Status u kuriera (najczęściej) w postaci indetyfikatora tekstowego</param>
        /// <param name="hasTrackingNumber">Informacja o tym, czy jest dostępny numer przewozowy</param>
        /// <returns>Status paczki w Sello NX</returns>
        public static StatusPaczki MapToApiStatus(string status, bool hasTrackingNumber)
        {
            switch (status)
            {
                case InPostCreated:
                case InPostConfirmed:
                    if (hasTrackingNumber)
                    {
                        return StatusPaczki.Stworzone;
                    }
                    else
                    {
                        return StatusPaczki.Tworzone;
                    }
                case InPostOffers_prepared:
                case InPostOffer_selected:
                    if (hasTrackingNumber)
                    {
                        return StatusPaczki.Stworzone;
                    }
                    else
                    {
                        return StatusPaczki.Tworzone;
                    }
                case InPostAdopted_at_sorting_center:
                    return StatusPaczki.BladTworzenia;
                case InPostDispatched_by_sender:
                case InPostReady_to_pickup_from_pok:
                case InPostReady_to_pickup_from_pok_registered:
                    return StatusPaczki.CzekaNaOdbior;
                case InPostCollected_from_sender:
                case InPostSent_from_source_branch:
                case InPostAdopted_at_source_branch:
                case InPostOversized:
                case InPostAdopted_at_target_branch:
                case InPostOut_for_delivery:
                case InPostDelivered:
                case InPostOut_for_delivery_to_address:
                case InPostDelay_in_delivery:
                case InPostRedirect_to_box:
                case InPostCanceled_redirect_to_box:
                case InPostReaddressed:
                case InPostStack_in_customer_service_point:
                case InPostUnstack_from_customer_service_point:
                case InPostStack_in_box_machine:
                case InPostUnstack_from_box_machine:
                    return StatusPaczki.WDrodze;
                case InPostTaken_by_courier:
                case InPostReturned_to_sender:
                case InPostTaken_by_courier_from_pok:
                case InPostReturn_pickup_confirmation_to_sender:
                case InPostTaken_by_courier_from_customer_service_point:
                    return StatusPaczki.WDrodzePowrotnej;
                case InPostSent_from_sorting_center:
                case InPostDispatched_by_sender_to_pok:
                    return StatusPaczki.CzekaNaKuriera;
                case InPostReady_to_pickup:
                case InPostPickup_reminder_sent:
                case InPostCourier_avizo_in_customer_service_point:
                    return StatusPaczki.Dostarczono;
                case InPostPickup_time_expired:
                case InPostAvizo:
                case InPostUndelivered:
                case InPostReady_to_pickup_from_branch:
                case InPostStack_parcel_pickup_time_expired:
                    return StatusPaczki.NieOdebrano;
                case InPostClaimed:
                case InPostMissing:
                    return StatusPaczki.BladTworzenia;
                case InPostCanceled:
                    return StatusPaczki.BladTworzenia;
                case InPostOther:
                    return StatusPaczki.BladTworzenia;
                case InPostPickup_reminder_sent_address:
                case InPostUndelivered_wrong_address:
                case InPostUndelivered_incomplete_address:
                case InPostUndelivered_unknown_receiver:
                case InPostUndelivered_cod_cash_receiver:
                case InPostUndelivered_no_mailbox:
                case InPostUndelivered_not_live_address:
                case InPostUndelivered_lack_of_access_letterbox:
                case InPostStack_parcel_in_box_machine_pickup_time_expired:
                    return StatusPaczki.WDrodzeBlad;
                case InPostRejected_by_receiver:
                    return StatusPaczki.NieOdebrano;
                default:
                    return StatusPaczki.BladTworzenia;
            }

        }
    }
}
