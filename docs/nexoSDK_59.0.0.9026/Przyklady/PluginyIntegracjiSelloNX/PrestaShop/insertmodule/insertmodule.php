<?php
if (!defined('_PS_VERSION_')) {
    exit;
}
require_once(_PS_MODULE_DIR_ . 'insertmodule/classes/InsertOrder.php');
require_once(_PS_MODULE_DIR_ . 'insertmodule/classes/WebserviceSpecificManagementInsertPayment.php');
require_once(_PS_MODULE_DIR_ . 'insertmodule/classes/InsertModuleWs.php');

class InsertModule extends Module
{
    public function __construct()
    {
        $this->name = 'insertmodule';
        $this->tab = 'other';
        $this->version = '1.0.0';
        $this->author = 'InsERT S.A.';
        $this->bootstrap = true;
        parent::__construct();
        $this->displayName = $this->l('InsERT');
        $this->description = $this->l('Moduł do obsługi Sello NX');
    }

    public function install()
    {
        parent::install();
        $this->registerHook('addWebserviceResources');
        return true;
    }

   	public function hookAddWebserviceResources() {
		return array(
			'insert_order' => array('description' => 'Rozszerzenie do zasobu Order', 'class' => 'InsertOrder'),
			'insert_payment' => array('description' => 'Zasób do nazwy płatności', 'specific_management' => true )
		);
	}
}