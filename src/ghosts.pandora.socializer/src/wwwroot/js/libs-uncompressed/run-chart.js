/*!
 * Chart.js Demo config options
 */

/*
*Helpers Functions
*/

// Numbers with zeros
function pad(d) {
	return (d < 10) ? '0' + d : d;
}

// Generate range array
function range(start, stop, step) {
	var a = [pad(start)], b = start;
	while (b < stop) {
		b += step;
		a.push(pad(b))
	}
	return a;
}


var twoBarChart = document.getElementById("two-bars-chart");
/*
 * Single Two Bar Graphic
 * 14-FavouritePage-Statistics.html
*/
if (twoBarChart !== null) {
	var ctx_tb = twoBarChart.getContext("2d");
	var data_tb = {
		labels: range(2011, 2016, 1),
		datasets: [
			{
				label: "Statistic 02",
				backgroundColor: "#ffdc1b",
				borderSkipped: "bottom",
				data: [43, 47, 38, 30, 47, 39]
			}, {
				label: "Statistic 01",
				backgroundColor: "#ff5e3a",
				borderSkipped: 'bottom',
				borderWidth: 0,
				data: [36, 30, 45, 50, 39, 41]
			}]
	};

	var twoBarChartEl = new Chart(ctx_tb, {
		type: 'bar',
		data: data_tb,
		options: {
			legend: {
				display: false
			},
			tooltips: {
				mode: 'index',
				intersect: false
			},
			responsive: true,
			scales: {
				xAxes: [{
					barPercentage: 0.7,
					gridLines: {
						display: false
					},
					ticks: {
						fontColor: '#888da8'
					}
				}],
				yAxes: [{
					stacked: true,
					gridLines: {
						display: false
					},
					ticks: {
						beginAtZero: true,
						fontColor: '#888da8'
					}
				}]
			}
		}
	});
}

var lineStackedChart = document.getElementById("line-stacked-chart");
/*
 *  Lines Graphic
 * 14-FavouritePage-Statistics.html
 */
if (lineStackedChart !== null) {
	var ctx_ls = lineStackedChart.getContext("2d");
	var data_ls = {
		labels: ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"],
		datasets: [
			{
				label: " - Favorites",
				backgroundColor: "rgba(57,169,255,0.35)",
				borderColor: "#38a9ff",
				borderWidth: 4,
				pointBorderColor: "#38a9ff",
				pointBackgroundColor: "#fff",
				pointBorderWidth: 4,
				pointRadius: 6,
				pointHoverRadius: 8,
				data: [98, 42, 38, 57, 82, 41, 36, 30, 45, 62, 64, 80]
			},
			{
				label: " - Visitors",
				backgroundColor: "rgba(8,221,123,0.2)",
				borderColor: "#08ddc1",
				borderWidth: 4,
				pointBorderColor: "#08ddc1",
				pointBackgroundColor: "#fff",
				pointBorderWidth: 4,
				pointRadius: 6,
				pointHoverRadius: 8,
				data: [78, 101, 80, 87, 120, 105, 110, 76, 101, 96, 100, 135]
			}]
	};

	var lineStackedEl = new Chart(ctx_ls, {
		type: 'line',
		data: data_ls,
		options: {
			legend: {
				display: false
			},
			responsive: true,
			scales: {
				xAxes: [{
					gridLines: {
						color: "#f0f4f9"
					},
					ticks: {
						fontColor: '#888da8'
					}
				}],
				yAxes: [{
					gridLines: {
						display: false
					},
					ticks: {
						beginAtZero: true,
						fontColor: '#888da8'
					}
				}]
			}
		}
	});
}
/*
 * Monthly Bar Graphic
 * 26-Statistics.html
 */

var oneBarChart = document.getElementById("one-bar-chart");
if (oneBarChart !== null) {
	var ctx_ob = oneBarChart.getContext("2d");
	var data_ob = {
		labels: range(1, 31, 1),
		datasets: [
			{
				backgroundColor: "#38a9ff",
				data: [9, 11, 8, 6, 13, 7, 7, 0, 9, 12, 7, 13, 12, 8, 1, 10, 9, 7, 3, 7, 10, 4, 14, 9, 6, 6, 11, 12, 3, 4, 2]
			},
			{
				backgroundColor: "#ebecf1",
				data: [11, 9, 12, 14, 7, 13, 13, 20, 11, 8, 13, 7, 8, 12, 19, 10, 11, 13, 17, 13, 10, 16, 6, 11, 14, 14, 9, 8, 17, 16, 18]
			}]
	};

	var oneBarEl = new Chart(ctx_ob, {
		type: 'bar',
		data: data_ob,

		options: {
			deferred: {           // enabled by default
				delay: 200        // delay of 500 ms after the canvas is considered inside the viewport
			},
			tooltips: {
				enabled: false
			},
			legend: {
				display: false
			},
			responsive: true,
			scales: {
				xAxes: [{
					stacked: true,
					barPercentage: 0.6,
					gridLines: {
						display: false
					},
					ticks: {
						fontColor: '#888da8'
					}
				}],
				yAxes: [{
					stacked: true,
					gridLines: {
						color: "#f0f4f9"
					},
					ticks: {
						beginAtZero: true,
						fontColor: '#888da8'
					}
				}]
			}
		}
	});
}

var lineGraphicChart = document.getElementById("line-graphic-chart");
/*
 *  Waves Graphic
 * 26-Statistics.html
 */
if (lineGraphicChart !== null) {
	var ctx_lg = lineGraphicChart.getContext("2d");
	var data_lg = {
		labels: ["Aug 8", "Aug 15", "Aug 21", "Aug 28", "Sep 4", "Sep 11", "Sep 19", "Sep 26", "Oct 3", "Oct 10", "Oct 16", "Oct 23", "Oct 30"],
		datasets: [
			{
				label: " - Favorites",
				backgroundColor: "rgba(255,215,27,0.6)",
				borderColor: "#ffd71b",
				borderWidth: 4,
				pointBorderColor: "#ffd71b",
				pointBackgroundColor: "#fff",
				pointBorderWidth: 4,
				pointRadius: 0,
				pointHoverRadius: 8,
				data: [98, 42, 38, 57, 82, 41, 36, 30, 45, 62, 64, 80, 68]
			},
			{
				label: " - Visitors",
				backgroundColor: "rgba(255,94,58,0.6)",
				borderColor: "#ff5e3a",
				borderWidth: 4,
				pointBorderColor: "#ff5e3a",
				pointBackgroundColor: "#fff",
				pointBorderWidth: 4,
				pointRadius: 0,
				pointHoverRadius: 8,
				data: [78, 101, 80, 87, 120, 105, 110, 76, 101, 96, 100, 115, 135]
			}]
	};

	var lineGraphicEl = new Chart(ctx_lg, {
		type: 'line',
		data: data_lg,
		options: {
			deferred: {           // enabled by default
				delay: 300        // delay of 500 ms after the canvas is considered inside the viewport
			},
			legend: {
				display: false
			},
			responsive: true,
			scales: {
				xAxes: [{
					gridLines: {
						color: "#f0f4f9"
					},
					ticks: {
						fontColor: '#888da8'
					}
				}],
				yAxes: [{
					gridLines: {
						display: false
					},
					ticks: {
						beginAtZero: true,
						fontColor: '#888da8'
					}
				}]
			}
		}
	});
}
var pieColorChart = document.getElementById("pie-color-chart");
/*
 *  Colors Pie Chart
 * 26-Statistics.html
 */
if (pieColorChart !== null) {
	var ctx_pc = pieColorChart.getContext("2d");
	var data_pc = {
		labels: ["Status Updates", "Multimedia", "Shared Posts", "Blog Posts"],
		datasets: [
			{
				data: [8.247, 5.630, 1.498, 1.136],
				borderWidth: 0,
				backgroundColor: [
					"#7c5ac2",
					"#08ddc1",
					"#ff5e3a",
					"#ffd71b"
				]
			}]
	};

	var pieColorEl = new Chart(ctx_pc, {
		type: 'doughnut',
		data: data_pc,
		options: {
			deferred: {           // enabled by default
				delay: 300        // delay of 500 ms after the canvas is considered inside the viewport
			},
			cutoutPercentage: 93,
			legend: {
				display: false
			},
			animation: {
				animateScale: false
			}
		}
	});
}
/*
 * Pie Chart with Text
 * 26-Statistics.html
 *
 * js/circle-progress.min.js
 *
 * https://github.com/kottenator/jquery-circle-progress
 *
 */

(function ($) {
	// USE STRICT
	"use strict";

	var $pie_chart = $('.pie-chart');
	$pie_chart.appear({force_process: true});
	$pie_chart.on('appear', function () {
		var current_cart = $(this);
		if (!current_cart.data('inited')) {
			var startColor = current_cart.data('startcolor');
			var endColor = current_cart.data('endcolor');
			var counter = current_cart.data('value') * 100;

			current_cart.circleProgress({
				thickness: 16,
				size: 360,
				startAngle: -Math.PI / 4 * 2,
				emptyFill: '#ebecf1',
				lineCap: 'round',
				fill: {
					gradient: [endColor, startColor],
					gradientAngle: Math.PI / 4
				}
			}).on('circle-animation-progress', function (event, progress) {
				current_cart.find('.content').html(parseInt(counter * progress, 10) + '<span>%</span>'
				)

			});
			current_cart.data('inited', true);
		}
	});

})(jQuery);


/*
 * Worldwide Statistics
 * 26-Statistics.html
 *
 * https://www.gstatic.com/charts/loader.js
 *
 * https://developers.google.com/chart/interactive/docs/gallery/geochart?csw=1#Configuration_Options
 *
 */

var USMapChart = document.getElementById("us-chart-map");
if (USMapChart !== null) {

	google.charts.load('current', {'packages': ['geochart']});
	google.charts.setOnLoadCallback(drawUSRegionsMap);

	function drawUSRegionsMap() {

		var data = google.visualization.arrayToDataTable([
			['City', 'Profile Visits', 'Post Likes'],
			['New York City', 276147, 12855],
			['Los Angeles', 135241, 18421],
			['Chicago', 9595, 1217],
			['Austin', 9063, 13360],
			['Washington', 276147, 12855],
			['Colorado', 95975, 15217]

		]);


		var options = {
			resolution: 'provinces',
			region: 'US',
			displayMode: 'markers',
			legend: 'none',
			colorAxis: {colors: ['#38a9ff', '#08ddc1']}
		};

		var chart = new google.visualization.GeoChart(USMapChart);
		chart.draw(data, options);
	}
}


var lineChart = document.getElementById("line-chart");
/*
 *  Yearly Line Graphic
 * 26-Statistics.html
 */
if (lineChart !== null) {
	var ctx_lc = lineChart.getContext("2d");
	var data_lc = {
		labels: ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"],
		datasets: [
			{
				label: " - Comments",
				borderColor: "#ffdc1b",
				borderWidth: 4,
				pointBorderColor: "#ffdc1b",
				pointBackgroundColor: "#fff",
				pointBorderWidth: 4,
				pointRadius: 6,
				pointHoverRadius: 8,
				fill: false,
				lineTension: 0,
				data: [96, 63, 136, 78, 111, 83, 101, 83, 102, 61, 45, 135]
			},
			{
				label: " - Likes",
				borderColor: "#08ddc1",
				borderWidth: 4,
				pointBorderColor: "#08ddc1",
				pointBackgroundColor: "#fff",
				pointBorderWidth: 4,
				pointRadius: 6,
				pointHoverRadius: 8,
				fill: false,
				lineTension: 0,
				data: [118, 142, 119, 123, 165, 139, 145, 116, 152, 123, 139, 195]
			}]
	};

	var lineChartEl = new Chart(ctx_lc, {
		type: 'line',
		data: data_lc,
		options: {
			legend: {
				display: false
			},
			responsive: true,
			scales: {
				xAxes: [{
					ticks: {
						fontColor: '#888da8'
					},
					gridLines: {
						color: "#f0f4f9"
					}
				}],
				yAxes: [{
					gridLines: {
						color: "#f0f4f9"
					},
					ticks: {
						beginAtZero: true,
						fontColor: '#888da8'
					}
				}]
			}
		}
	});
}


var pieSmallChart = document.getElementById("pie-small-chart");
/*
 *  Colors Pie Chart
 * 26-Statistics.html
 */

if (pieSmallChart !== null) {
	var ctx_sc = pieSmallChart.getContext("2d");
	var data_sc = {
		labels: ["Yearly Likes", "Yearly Comments"],
		datasets: [
			{
				data: [65.048, 42.973],
				borderWidth: 0,
				backgroundColor: [
					"#08ddc1",
					"#ffdc1b"
				]
			}]
	};

	var pieSmallEl = new Chart(ctx_sc, {
		type: 'doughnut',
		data: data_sc,
		options: {
			deferred: {           // enabled by default
				delay: 300        // delay of 500 ms after the canvas is considered inside the viewport
			},
			cutoutPercentage: 93,
			legend: {
				display: false
			},
			animation: {
				animateScale: false
			}
		}
	});
}


var twoBar2Chart = document.getElementById("two-bar-chart-2");
/*
 *  Colors Pie Chart
 * 26-Statistics.html
 */

if (twoBar2Chart !== null) {
	var ctx_tb2 = twoBar2Chart.getContext("2d");
	var data_tb2 = {
		labels: range(2011, 2016, 1),
		datasets: [
			{
				label: "Facebook",
				backgroundColor: "#2f5b9d",
				borderSkipped: "bottom",
				data: [43, 47, 38, 30, 47, 39]
			}, {
				label: "Twitter",
				backgroundColor: "#38bff1",
				borderSkipped: 'bottom',
				borderWidth: 0,
				data: [36, 30, 45, 50, 39, 41]
			}]
	};

	var twoBar2ChartEl = new Chart(ctx_tb2, {
		type: 'bar',
		data: data_tb2,
		options: {
			legend: {
				display: false
			},
			tooltips: {
				mode: 'index',
				intersect: false
			},
			responsive: true,
			scales: {
				xAxes: [{
					barPercentage: 0.5,
					gridLines: {
						display: false
					},
					ticks: {
						fontColor: '#888da8'
					}
				}],
				yAxes: [{
					gridLines: {
						display: false
					},
					ticks: {
						beginAtZero: true,
						fontColor: '#888da8'
					}
				}]
			}
		}
	});
}

var radarChart = document.querySelectorAll(".radar-chart");
/*
 *  Radar Chart
 * 26-Statistics.html
 */

for (var i = 0; i < radarChart.length; i++) {

	var ctx_rc = radarChart[i].getContext("2d");
	var data_rc = {
		datasets: [{
			data: [
				11,
				16,
				26
			],
			backgroundColor: [
				"#38a9ff",
				"#ff5e3a",
				"#ffdc1b"
			]
		}],
		labels: [
			"Blue",
			"Orange",
			"Yellow"

		]
	};

	var radarChartEl = new Chart(ctx_rc, {
		type: 'pie',
		data: data_rc,
		options: {
			deferred: {           // enabled by default
				delay: 300        // delay of 500 ms after the canvas is considered inside the viewport
			},
			legend: {
				display: false
			},
			scale: {
				gridLines: {
					display: false
				},
				ticks: {
					beginAtZero: true
				},
				reverse: false
			},
			animation: {
				animateScale: true
			}
		}
	});

}