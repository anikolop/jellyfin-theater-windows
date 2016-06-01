﻿define(['loading', 'pluginManager'], function (loading, pluginManager) {

    return function (view, params) {

        var self = this;

        view.addEventListener('viewbeforeshow', function (e) {

            var isRestored = e.detail.isRestored;

            Emby.Page.setTitle('Windows Player');

            loading.hide();

            if (!isRestored) {
                renderSettings();

                view.querySelector('.btnMadvr').addEventListener('click', onMadvrClick);
                view.querySelector('.btnReset').addEventListener('click', onResetClick);
            }
        });

        view.addEventListener('viewbeforehide', function (e) {

            saveSettings();
        });

        function onMadvrClick() {
            Emby.Page.show(pluginManager.mapRoute('directshowplayer', 'directshowplayer/madvr.html'));
        }

        function onResetClick() {
            return sendCommand('configresetdefaults-video');
        }

        function saveSettings() {

            getConfiguration().then(function (config) {

                var selectHwaMode = view.querySelector('.selectHwaMode');
                config.VideoConfig.HwaMode = selectHwaMode.value;

                var selectVideoRenderer = view.querySelector('.selectVideoRenderer');
                config.VideoConfig.EnableMadvr = selectVideoRenderer.value == 'madVR';
                config.VideoConfig.EnableCustomPresenter = selectVideoRenderer.value == 'EVRCP';

                var selectRefreshRateMode = view.querySelector('.selectRefreshRateMode');
                config.VideoConfig.AutoChangeRefreshRate = selectRefreshRateMode.value;

                var selectFilterSet = view.querySelector('.selectFilterSet');
                config.FilterSet = selectFilterSet.value;

                var selectNominalRange = view.querySelector('.selectNominalRange');
                config.VideoConfig.NominalRange = selectNominalRange.value;

                saveConfiguration(config);
            });

        }

        function renderSettings() {

            getConfiguration().then(function (config) {

                var selectHwaMode = view.querySelector('.selectHwaMode');
                selectHwaMode.value = config.VideoConfig.HwaMode;

                var selectVideoRenderer = view.querySelector('.selectVideoRenderer');
                var videoRenderer = config.VideoConfig.EnableMadvr ? 'madVR' : config.VideoConfig.EnableCustomPresenter ? 'EVRCP' : 'EVR';
                selectVideoRenderer.value = videoRenderer;

                var selectRefreshRateMode = view.querySelector('.selectRefreshRateMode');
                selectRefreshRateMode.value = config.VideoConfig.AutoChangeRefreshRate;

                var selectFilterSet = view.querySelector('.selectFilterSet');
                selectFilterSet.value = config.FilterSet;

                var selectNominalRange = view.querySelector('.selectNominalRange');
                selectNominalRange.value = config.VideoConfig.NominalRange;
            });
        }

        function getConfiguration() {

            return sendCommand('config');
        }

        function saveConfiguration(config) {

            return sendCommand('configsave', config);
        }

        function sendCommand(name, body) {

            var request = {
                type: 'POST',
                url: 'http://localhost:8154/directshowplayer/' + name,
                dataType: 'json'
            };

            if (body) {
                request.contentType = 'application/json';
                request.data = JSON.stringify(body);
            }

            return getFetchPromise(request).then(function (response) {

                return response.json();

            });
        }
        function getFetchPromise(request) {

            var headers = request.headers || {};

            if (request.dataType == 'json') {
                headers.accept = 'application/json';
            }

            var fetchRequest = {
                headers: headers,
                method: request.type
            };

            var contentType = request.contentType;

            if (request.data) {

                if (typeof request.data === 'string') {
                    fetchRequest.body = request.data;
                } else {
                    fetchRequest.body = paramsToString(request.data);

                    contentType = contentType || 'application/x-www-form-urlencoded; charset=UTF-8';
                }
            }

            if (contentType) {

                headers['Content-Type'] = contentType;
            }

            return fetch(request.url, fetchRequest);
        }

        function paramsToString(params) {

            var values = [];

            for (var key in params) {

                var value = params[key];

                if (value !== null && value !== undefined && value !== '') {
                    values.push(encodeURIComponent(key) + "=" + encodeURIComponent(value));
                }
            }
            return values.join('&');
        }
    }

});