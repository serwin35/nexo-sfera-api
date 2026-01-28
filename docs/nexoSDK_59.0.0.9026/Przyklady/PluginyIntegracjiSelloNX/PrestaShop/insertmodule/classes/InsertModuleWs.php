<?php

class InsertModuleWs extends ObjectModel
{
    public $name;
    public $id_module;

    public static $definition = 
        array(
            'table' => 'module',
            'primary' => 'id_module',
            'fields' => array(
                'name' => array('validate' => 'isString', 'type' => self::TYPE_STRING, 'required' => true)
            ),
    );

    protected $webserviceParameters = [
        'objectNodeName' => 'insert_payment',
        'objectsNodeName' => 'insert_payments',
        'fields' => [
            'descritpion' => ['getter' => 'getDescriptionPaymentModule'],
            'SNXPaymentType' => ['getter' => 'getSNXPaymentType']
        ],
    ];

    public function getSNXPaymentType()
    {
        if($this->name === 'ps_wirepayment')
            return 'PRZELEW';
        else if($this->name === 'ps_cashondelivery')
            return 'GOTOWKA';
        else if($this->name === 'przelewy24')
            return 'P24';
        else return $this->name;
    }

    public function getDescriptionPaymentModule()
    {
        return Module::getInstanceByName($this->name)->displayName;
    }
    
    public function getId()
    {
        return $this->id_module;
    }
}