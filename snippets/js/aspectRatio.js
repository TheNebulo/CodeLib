var acceptOnlyValidAspectRatios = true; // This variable dictates whether invalid aspect ratios (anything that isn't around 16:9) are redirected to non-compatible.html. Turn it off during development only.
var oldSheet = "desktop.css";
var heightGain = 1.1;
var updateSizes;

function getAspectRatioSheet() {
	var aspectRatio = window.innerWidth / window.innerHeight;
	var aspectRatios = {
		"desktop.css":2,
		"mobile.css": 0.5
	};

	var closestDifference = Number.MAX_VALUE;
	var closestCssFile = null;

	for (var cssFile in aspectRatios) {
		var diff = Math.abs(aspectRatio - aspectRatios[cssFile]);
		if (diff < closestDifference) {
			closestDifference = diff;
			closestCssFile = cssFile;
		}
	}

	if(closestDifference <= 0.35) {
		return closestCssFile;
	}
	else {
		return null;
	}
}

function swapStyleSheet(sheet) {
	let old = document.getElementById("pagestyle");
	let link = document.createElement('link');
	link.rel = 'stylesheet';
	link.href = sheet;
	link.id = "pagestyle"

	link.onload = function() {
		old.remove();
		setInterval(() => {
			updateSizes();
		}, "250ms"); 
	};

	var head = document.getElementsByTagName('head')[0];
	head.appendChild(link);
}

setInterval(() => {
	var sheet = getAspectRatioSheet();
	if(sheet === null && acceptOnlyValidAspectRatios) href("/not-compatible.html");
	if (sheet && sheet !== oldSheet) {
		if(sheet === "mobile.css") { heightGain = 0.8; }
		else { heightGain = 1.1; }
		oldSheet = sheet;
		swapStyleSheet("/css/main/"+sheet); 
	}
}, 100);