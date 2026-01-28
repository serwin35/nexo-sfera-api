<?php

class WebserviceSpecificManagementInsertPayment  implements WebserviceSpecificManagementInterface
{
    protected $objOutput;
    protected $wsObject;
    protected $output;
    protected $urlSegment;

    public function getContent()
    {
        return $this->objOutput->getObjectRender()->overrideContent($this->output);
    }

    public function setUrlSegment($segments)
    {
        $this->urlSegment = $segments;
        return $this;
    }

    public function getUrlSegment()
    {
        return $this->urlSegment;
    }

    public function setObjectOutput(WebserviceOutputBuilderCore $obj)
    {
        $this->objOutput = $obj;
        return $this;
    }

    public function getObjectOutput()
    {
        return $this->objOutput;
    }

    public function setWsObject(WebserviceRequestCore $obj)
    {
        $this->wsObject = $obj;
        return $this;
    }

    public function getWsObject()
    {
        return $this->wsObject;
    }

    public function manage()
    {
        $this->wsObject->fieldsToDisplay = 'full';

        $sql = 'SELECT hm.id_module FROM `' . _DB_PREFIX_ . 'hook_module` hm 
        INNER JOIN `' . _DB_PREFIX_ . 'module` m ON m.id_module=hm.id_module
        INNER JOIN `' . _DB_PREFIX_ . 'hook` h ON h.name="paymentOptions" AND h.id_hook=hm.id_hook
        WHERE m.active=1 
        GROUP BY m.id_module';
        $payments = Db::getInstance()->executeS($sql);

        $objects = array();
        $objects['empty'] = new InsertModuleWs();
        
        foreach ($payments as $payment) {
            $objects[] = new InsertModuleWs($payment['id_module']);
        }

        $this->_resourceConfiguration = $objects['empty']->getWebserviceParameters();

        $this->output = $this->objOutput->getContent(
        $objects,
        null,
        $this->wsObject->fieldsToDisplay,
        $this->wsObject->depth,
        WebserviceOutputBuilderCore::VIEW_LIST,
        false
        );
    }
}