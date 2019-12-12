//===============================================================================
// Microsoft FastTrack for Azure
// Logic App to Azure Automation Sample
//===============================================================================
// Copyright © Microsoft Corporation.  All rights reserved.
// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY
// OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND
// FITNESS FOR A PARTICULAR PURPOSE.
//===============================================================================

// Define the Okta namespace
var Okta = (function ($) {
    var init = function () {

    };

    return {
        init: init
    };
})(jQuery);

// Define the Okta.Core namespace
Okta.Core = (function ($) {
    var _functionUrl = '';
    var _functionKey = '';
    var _userName = '';
    var _ipAddress = '';

    var getSASToken = function (functionUrl, functionKey, queueUrl, queueName, virtualMachineName, callBack) {
        var sasToken = '';
        $.support.cors = true; // Enable CORS support
        $.ajax({
            type: "POST",
            url: functionUrl,
            crossDomain: true, // CORS
            headers: {
                'x-functions-key': functionKey
            },
            data: '{"queueName":"' + queueName + '"}'
        }).done(function (data) {
            sasToken = data;
            console.log("Azure Function successfully returned SAS token for queue: " + queueName);
            callBack(queueUrl, queueName, virtualMachineName, sasToken);
        }).fail(function (jqXHR, textStatus) {
            console.log("Error calling the Azure Function:\n" + textStatus);
            callBack(queueUrl, queueName, virtualMachineName, sasToken);
        });
    };

    var deployVM = function (queueUrl, queueName, virtualMachineName, sasToken) {
        if (sasToken !== '') {
            var queueService = AzureStorage.Queue.createQueueServiceWithSas(queueUrl, sasToken);
            var encoder = new AzureStorage.Queue.QueueMessageEncoder.TextBase64QueueMessageEncoder();
            queueService.createMessage(queueName, encoder.encode(virtualMachineName), function (error, results, response) {
                if (error) {
                    // Create message error
                    console.log("Error writing to queue " + queueName + ": " + error);
                } else {
                    // Create message successfully
                    $('#divVirtualMachineTable').show();
                    $('#btnRequestVM').attr('disabled', 'disabled');
                    $('#btnCheckStatus').removeAttr('disabled');
                    $('#spnVirtualMachineName').html(virtualMachineName);
                    $('#spnStatus').html('Processing');
                    $('#txtVirtualMachineName').val('');
                    $('#hidVirtualMachineName').val(virtualMachineName);
                    console.log("Message successfully written to queue: " + queueName);
                }
            });
        }
    };

    var checkVMDeployment = function (queueUrl, queueName, virtualMachineName, sasToken) {
        if (sasToken !== '') {
            var queueService = AzureStorage.Queue.createQueueServiceWithSas(queueUrl, sasToken);
            queueService.getMessages(queueName, function (error, result, response) {
                if (error) {
                    console.log("Error getting messages from " + queueName + ": " + error);
                }
                else {
                    if (result.length > 0) {
                        var message = result[0];
                        $('#btnCheckStatus').attr('disabled', 'disabled');
                        $('#spnStatus').html('Ready');
                        $('#btnRequestAccess').show();
                        queueService.deleteMessage(queueName, message.messageId, message.popReceipt, function (error, response) {
                            if (error) {
                                console.log("Error deleting message from " + queueName + ": " + error);
                            }
                            else {
                                console.log("Message deleted from queue: " + queueName);
                            }
                        });
                    }
                }
            });
        }
    };

    var accessVM = function (queueUrl, queueName, virtualMachineName, sasToken) {
        if (sasToken !== '') {
            var queueService = AzureStorage.Queue.createQueueServiceWithSas(queueUrl, sasToken);
            var encoder = new AzureStorage.Queue.QueueMessageEncoder.TextBase64QueueMessageEncoder();
            var message = '{"userName":"' + _userName + '","virtualMachineName":"' + virtualMachineName + '","ipAddress":"' + _ipAddress + '"}';
            queueService.createMessage(queueName, encoder.encode(message), function (error, results, response) {
                if (error) {
                    // Create message error
                    console.log("Error writing to queue" + queueName + ": " + error);
                } else {
                    // Create message successfully
                    $('#spnStatus').html('Requesting Access');
                    $('#btnRequestAccess').hide();
                    $('#btnCheckAccess').show();
                    console.log("Message successfully written to queue: " + queueName);
                }
            });
        }
    };

    var checkVMAccess = function (queueUrl, queueName, virtualMachineName, sasToken) {
        if (sasToken !== '') {
            var queueService = AzureStorage.Queue.createQueueServiceWithSas(queueUrl, sasToken);
            queueService.getMessages(queueName, function (error, result, response) {
                if (error) {
                    console.log("Error getting messages from " + queueName + ": " + error);
                }
                else {
                    if (result.length > 0) {
                        var message = result[0];
                        $('#btnCheckAccess').hide();
                        $('#spnStatus').hide();
                        $('#lnkRDP').attr('href', './rdp/' + virtualMachineName);
                        $('#lnkRDP').show();
                        queueService.deleteMessage(queueName, message.messageId, message.popReceipt, function (error, response) {
                            if (error) {
                                console.log("Error deleting message from " + queueName + ": " + error);
                            }
                            else {
                                console.log("Message deleted from queue: " + queueName);
                            }
                        });
                    }
                }
            });
        }
    };

    // Initialize the functionality of the Index page
    var init = function (functionUrl, functionKey, queueUrl, userName, ipAddress) {
        _functionUrl = functionUrl;
        _functionKey = functionKey;
        _userName = userName;
        _ipAddress = ipAddress;
        $('#btnRequestVM').on('click', function () {
            var virtualMachineName = $('#txtVirtualMachineName').val();
            if (virtualMachineName !== '') {
                getSASToken(_functionUrl, _functionKey, queueUrl, 'deployvminputqueue', virtualMachineName, deployVM);
            }
        });
        $('#btnCheckStatus').on('click', function () {
            var virtualMachineName = $('#hidVirtualMachineName').val();
            if (virtualMachineName !== '') {
                getSASToken(_functionUrl, _functionKey, queueUrl, 'deployvmoutputqueue', virtualMachineName, checkVMDeployment);
            }
        });
        $('#btnRequestAccess').on('click', function () {
            var virtualMachineName = $('#hidVirtualMachineName').val();
            if (virtualMachineName !== '') {
                getSASToken(_functionUrl, _functionKey, queueUrl, 'accessvminputqueue', virtualMachineName, accessVM);
            }
        });
        $('#btnCheckAccess').on('click', function () {
            var virtualMachineName = $('#hidVirtualMachineName').val();
            if (virtualMachineName !== '') {
                getSASToken(_functionUrl, _functionKey, queueUrl, 'accessvmoutputqueue', virtualMachineName, checkVMAccess);
            }
        });
    };

    // Expose the init method
    return {
        init: init
    };
})(jQuery);