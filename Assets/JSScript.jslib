mergeInto(LibraryManager.library,
{
	UnityLoaded: function () {
		console.log("Handle JS Message Ready")
    	if(window.onUnityLoaded !== undefined)
			window.onUnityLoaded()
		else
			console.log("window.onUnityLoaded is undefined")
  	},
	SetLocalStorge: function(name, value) {
		localStorage.setItem(UTF8ToString(name), UTF8ToString(value));
	},
	GetLocalStorge: function(name) {
		var returnStr = localStorage.getItem(UTF8ToString(name));
		if(returnStr == null || returnStr == undefined)
			return null;
		var bufferSize = lengthBytesUTF8(returnStr) + 1;
    	var buffer = _malloc(bufferSize);
    	stringToUTF8(returnStr, buffer, bufferSize);
    	return buffer;
	},
	NotifyPlaybackState: function(statePtr, time, duration) {
		var state = UTF8ToString(statePtr);
		if(window.onPlaybackState !== undefined)
			window.onPlaybackState(state, time, duration);
	},
	NotifyChartInfo: function(jsonPtr) {
		var json = UTF8ToString(jsonPtr);
		if(window.onChartInfo !== undefined)
			window.onChartInfo(JSON.parse(json));
	},
	NotifyNoteCount: function(jsonPtr) {
		var json = UTF8ToString(jsonPtr);
		if(window.onNoteCount !== undefined)
			window.onNoteCount(JSON.parse(json));
	},
	NotifyComboStatus: function(jsonPtr) {
		var json = UTF8ToString(jsonPtr);
		if(window.onComboStatus !== undefined)
			window.onComboStatus(JSON.parse(json));
	},
	NotifySettings: function(jsonPtr) {
		var json = UTF8ToString(jsonPtr);
		if(window.onSettings !== undefined)
			window.onSettings(JSON.parse(json));
	},
	NotifyChartReloaded: function(success) {
		if(window.onChartReloaded !== undefined)
			window.onChartReloaded(!!success);
	}
});