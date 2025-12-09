<?php

require('inc/MailChimp.php');

use \DrewM\MailChimp\MailChimp;

class Crumina_Submit {

    private $message         = '';
    private $email_subject   = '';
    private $email           = '';
    private $message_success = '';
    private $nonce           = null;
    private $type            = null;
    private $config          = null;
    private $inputs_allowed   = null;
    private $inputs_required = null;
    private $inputs          = array();

    public function __construct() {
        $this->config = require('inc/config.php');

        $this->getData();
        $this->validateData();

        if ( $this->type === 'standard' ) {
            $this->prepareStandardMessage();
            $this->sendStandardMessage();
        }

        if ( $this->type === 'mailchimp' ) {
            $this->sendMailchimpMessage();
        }
    }

    private function getData() {
        parse_str( filter_input( INPUT_POST, 'inputs' ), $this->inputs );
        $this->nonce = filter_input( INPUT_POST, 'nonce', FILTER_SANITIZE_STRING );
        $this->type  = filter_input( INPUT_POST, 'type', FILTER_SANITIZE_STRING );
    }

    private function validateData() {
        if ( $this->nonce !== 'crumina-submit-form-nonce' ) {
            throw new Exception( 'No direct eccess!' );
        }

        $this->inputs_required = isset( $this->config[ 'forms' ][ $this->type ][ 'inputs_required' ] ) ? (array) $this->config[ 'forms' ][ $this->type ][ 'inputs_required' ] : array();
        $this->inputs_allowed   = isset( $this->config[ 'forms' ][ $this->type ][ 'inputs_allowed' ] ) ? (array) $this->config[ 'forms' ][ $this->type ][ 'inputs_allowed' ] : array();
        $this->message_success = isset( $this->config[ 'forms' ][ $this->type ][ 'message_success' ] ) ? $this->config[ 'forms' ][ $this->type ][ 'message_success' ] : false;

        if ( !$this->inputs_allowed ) {
            throw new Exception( 'No allowed fields!' );
        }

        if ( !$this->message_success ) {
            throw new Exception( 'No success message!' );
        }

        if ( !$this->inputs ) {
            throw new Exception( 'No fields for submit!' );
        }

        foreach ( $this->inputs as $key => $input ) {
            switch ( $key ) {
                case 'email':
                    $filtered = filter_var( $input, FILTER_VALIDATE_EMAIL );
                    break;
                case 'website':
                    $filtered = filter_var( $input, FILTER_VALIDATE_URL );
                    break;
                default:
                    $filtered = filter_var( $input, FILTER_SANITIZE_STRING );
            }

            if ( !$filtered && in_array( $key, $this->inputs_required ) ) {
                throw new Exception( ucfirst( $key ) . ' field is empty!' );
            }

            if ( $filtered && in_array( $key, $this->inputs_allowed ) ) {
                $this->inputs[ $key ] = $filtered;
            } else {
                unset( $this->inputs[ $key ] );
            }
        }
    }

    private function prepareStandardMessage() {
        $this->email_subject   = isset( $this->config[ 'forms' ][ $this->type ][ 'email_subject' ] ) ? $this->config[ 'forms' ][ $this->type ][ 'email_subject' ] : false;
        $this->email           = isset( $this->config[ 'forms' ][ $this->type ][ 'email' ] ) ? $this->config[ 'forms' ][ $this->type ][ 'email' ] : false;
        
        if ( !$this->email ) {
            throw new Exception( 'No config email!' );
        }

        if ( !$this->email_subject ) {
            throw new Exception( 'No message subject!' );
        }
        
        if ( isset( $this->inputs[ 'email_subject' ] ) ) {
            $this->email_subject = $this->inputs[ 'email_subject' ] ? $this->inputs[ 'email_subject' ] : $this->email_subject;
        }

        foreach ( $this->inputs as $key => $field ) {
            $this->message .= '\r\n<p><strong>' . ucfirst( $key ) . ':</strong> ' . $field . '</p>\r\n';
        }
    }

    private function sendStandardMessage() {
        $headers = "MIME-Version: 1.0\r\n";
        $headers .= "Content-type: text/html; charset=UTF-8\r\n";
        $headers .= "From: {$this->inputs[ 'name' ]} <{$this->inputs[ 'email' ]}>\r\n";

        $submit = mail( $this->email, $this->email_subject, $this->message, $headers );

        if ( $submit ) {
            echo json_encode( array(
                'success' => true,
                'message' => $this->message_success
            ) );
        } else {
            throw new Exception( 'Have errors during submit!' );
        }
    }

    private function sendMailchimpMessage() {
        $api_key = isset( $this->config[ 'forms' ][ $this->type ][ 'api_key' ] ) ? $this->config[ 'forms' ][ $this->type ][ 'api_key' ] : false;
        $list_id = isset( $this->config[ 'forms' ][ $this->type ][ 'list_id' ] ) ? $this->config[ 'forms' ][ $this->type ][ 'list_id' ] : false;

        if ( !$api_key ) {
            throw new Exception( 'Api key is missing!' );
        }

        if ( !$list_id ) {
            throw new Exception( 'List id is missing!' );
        }

        $MailChimp = new MailChimp( $api_key );
        $subscribe = $MailChimp->post( "lists/{$list_id}/members", [
            'email_address' => $this->inputs[ 'email' ],
            'status'        => 'subscribed',
        ] );

        switch ( $subscribe[ 'status' ] ) {
            case 'subscribed':
                echo json_encode( array(
                    'success' => true,
                    'message' => $this->message_success
                ) );
                break;
            case 400:
                echo json_encode( array(
                    'success' => false,
                    'message' => $subscribe[ 'detail' ]
                ) );
                break;
            default:
                echo json_encode( array(
                    'success' => false,
                    'message' => 'Something went wrong!'
                ) );
        }
    }

}

try {
    new Crumina_Submit();
} catch ( Exception $e ) {
    echo json_encode( array(
        'success' => false,
        'message' => $e->getMessage()
    ) );
}