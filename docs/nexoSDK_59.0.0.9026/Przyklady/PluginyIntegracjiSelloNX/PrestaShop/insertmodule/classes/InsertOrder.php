<?php
class InsertOrder extends Order {

	public function __construct($id = null, $idLang = null) {
		if (isset($_SERVER['REQUEST_METHOD']) and $_SERVER['REQUEST_METHOD'] == 'GET')
		{
			// Deklaracja dodatkowych zmiennych w zasobie insert_order
			self::$definition['fields']['insert_method_send'] = self::TYPE_STRING;
			self::$definition['fields']['insert_parcel_locker'] = self::TYPE_STRING;
			self::$definition['fields']['insert_cod'] = self::TYPE_STRING;
			self::$definition['fields']['insert_address_delivery'] = self::TYPE_STRING;
			self::$definition['fields']['insert_address_invoice'] = self::TYPE_STRING;
			self::$definition['fields']['insert_customer'] = self::TYPE_STRING;
			self::$definition['fields']['insert_delivery_country'] = self::TYPE_STRING;
			self::$definition['fields']['insert_invoice_country'] = self::TYPE_STRING;
			self::$definition['fields']['insert_carrier'] = self::TYPE_STRING;
		}
		parent::__construct($id, $idLang);

		$this->insert_address_delivery = new Address($this->id_address_delivery);
		$this->insert_address_invoice = new Address($this->id_address_invoice);
		$this->insert_carrier = new Carrier($this->id_carrier);
		$this->insert_customer = new Customer($this->id_customer);
		$this->insert_parcel_locker = "";

		if(isset($this->insert_address_delivery))
		{
			$this->insert_delivery_country = new Country($this->insert_address_delivery->id_country);
		}

		if(isset($this->insert_address_invoice))
		{
			$this->insert_invoice_country = new Country($this->insert_address_invoice->id_country);
		}

		$carrierQuery = Db::getInstance(_PS_USE_SQL_SLAVE_)->executeS(
			'SELECT external_module_name, name FROM `' . _DB_PREFIX_ . 'carrier` WHERE id_carrier = ' . (int)$this->id_carrier);
		
		$carrierExternalModule =  isset($carrierQuery[0]) ? $carrierQuery[0]['external_module_name'] : '';	
		
		if($this->module == "ps_cashondelivery")
		{
			$this->insert_cod = "1";
		}
		else
		{
			$this->insert_cod = "0";
		}

		// Zewnętrzne moduły kurierskie i paczkoPunkty
		if(isset($carrierQuery[0]))
		{
			$this->insert_method_send = $carrierQuery[0]['name'];
			
			// Inpost
			if (Module::isInstalled('inpostshipping') and Module::isEnabled('inpostshipping') and isset($carrierExternalModule) and $carrierExternalModule == "inpostshipping" )
			{
				$this->insert_parcel_locker = $this->getWsInPostPoint();
			}

			// GLS
			else if(Module::isInstalled('glspoland') and Module::isEnabled('glspoland') and isset($carrierExternalModule) and $carrierExternalModule == "glspoland")
			{
				 $glsPointQuery = Db::getInstance(_PS_USE_SQL_SLAVE_)->executeS(
				 'SELECT parcel_shops FROM `' . _DB_PREFIX_ . 'gls_poland_checkout_session` WHERE id_cart = ' . (int)$this->id_cart);

				 if(isset($glsPointQuery[0]))
				 {
					$glsJson = json_decode($glsPointQuery[0]['parcel_shops'], true);
				 	$this->insert_parcel_locker = reset($glsJson);
		   		 }			
			}
			// Orlen paczka
			else if(Module::isInstalled('ruch') and Module::isEnabled('ruch') and $carrierExternalModule == "ruch")
			{
				$ruchPointQuery = Db::getInstance(_PS_USE_SQL_SLAVE_)->executeS(
				'SELECT point FROM `' . _DB_PREFIX_ . 'ruch_cart_point` WHERE id_cart = ' . (int)$this->id_cart);
				
				if(isset($ruchPointQuery[0]))
				{
					$this->insert_parcel_locker = $ruchPointQuery[0]['point'];
				}
			}

			// DPD
			else if(Module::isInstalled('dpdshipping') and Module::isEnabled('dpdshipping') and $carrierExternalModule == "dpdshipping")
			{
				$dpdPointQuery = Db::getInstance(_PS_USE_SQL_SLAVE_)->executeS(
				'SELECT pudo_code FROM `' . _DB_PREFIX_ . 'dpdshipping_cart_pickup` WHERE id_cart = ' . (int)$this->id_cart);
				
				if(isset($dpdPointQuery[0]))
				{
					$this->insert_parcel_locker = $dpdPointQuery[0]['pudo_code'];
				}
			}
			
			// DHL
			else if(Module::isInstalled('dhlassistant') and Module::isEnabled('dhlassistant') and $carrierExternalModule == "dhlassistant")
			{
				$dhlPointQuery = Db::getInstance(_PS_USE_SQL_SLAVE_)->executeS(
				'SELECT ParcelIdent FROM `' . _DB_PREFIX_ . 'dhla_sourceorderadditionalparams` WHERE idsourceobject = ' . (int)$this->id_cart . ' order by id desc');
				
				if(isset($dhlPointQuery[0]))
				{
					$this->insert_parcel_locker = $dhlPointQuery[0]['ParcelIdent'];
				}
			}
		}
	}
}
