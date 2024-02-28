// https://observablehq.com/@d3/temporal-force-directed-graph@258
import define1 from "./450051d7f1174df8@254.js";

function _1(md){return(
md``
)}

function _time(Scrubber,times){return(
Scrubber(times, {
  delay: 100, 
  loop: true,
  format: date => date.toLocaleString("en", {
    month: "long", 
    day: "numeric",
    hour: "numeric",
    minute: "numeric",
    timeZone: "UTC"
  })
})
)}

function _chart(d3,width,height,invalidation,drag)
{
  const simulation = d3.forceSimulation()
      .force("charge", d3.forceManyBody())
      .force("link", d3.forceLink().id(d => d.id))
      .force("x", d3.forceX())
      .force("y", d3.forceY())
      .on("tick", ticked);

  const svg = d3.create("svg")
      .attr("viewBox", [-width / 2, -height / 2, width, height]);

  let link = svg.append("g")
      .attr("stroke", "#999")
      .attr("stroke-opacity", 0.6)
    .selectAll("line");

  let node = svg.append("g")
      .attr("stroke", "#fff")
      .attr("stroke-width", 1.5)
    .selectAll("circle");

  function ticked() {
    node.attr("cx", d => d.x)
        .attr("cy", d => d.y);

    link.attr("x1", d => d.source.x)
        .attr("y1", d => d.source.y)
        .attr("x2", d => d.target.x)
        .attr("y2", d => d.target.y);
  }

  invalidation.then(() => simulation.stop());

  return Object.assign(svg.node(), {
    update({nodes, links}) {

      // Make a shallow copy to protect against mutation, while
      // recycling old nodes to preserve position and velocity.
      const old = new Map(node.data().map(d => [d.id, d]));
      nodes = nodes.map(d => Object.assign(old.get(d.id) || {}, d));
      links = links.map(d => Object.assign({}, d));

      node = node
        .data(nodes, d => d.id)
        .join(enter => enter.append("circle")
          .attr("r", 5)
          .call(drag(simulation))
          .call(node => node.append("title").text(d => d.id)));

      link = link
        .data(links, d => [d.source, d.target])
        .join("line");

      simulation.nodes(nodes);
      simulation.force("link").links(links);
      simulation.alpha(1).restart().tick();
      ticked(); // render now!
    }
  });
}


function _update(data,contains,time,chart)
{
  const nodes = data.nodes.filter(d => contains(d, time));
  const links = data.links.filter(d => contains(d, time));
  chart.update({nodes, links});
}


async function _data(FileAttachment){return(
JSON.parse(await FileAttachment("sfhh@4.json").text(), (key, value) => key === "start" || key === "end" ? new Date(value) : value)
)}

function _times(d3,data,contains){return(
d3.scaleTime()
  .domain([d3.min(data.nodes, d => d.start), d3.max(data.nodes, d => d.end)])
  .ticks(1000)
  .filter(time => data.nodes.some(d => contains(d, time)))
)}

function _contains(){return(
({start, end}, time) => start <= time && time < end
)}

function _height(){return(
680
)}

function _drag(d3){return(
simulation => {
  
  function dragstarted(event, d) {
    if (!event.active) simulation.alphaTarget(0.3).restart();
    d.fx = d.x;
    d.fy = d.y;
  }
  
  function dragged(event, d) {
    d.fx = event.x;
    d.fy = event.y;
  }
  
  function dragended(event, d) {
    if (!event.active) simulation.alphaTarget(0);
    d.fx = null;
    d.fy = null;
  }
  
  return d3.drag()
      .on("start", dragstarted)
      .on("drag", dragged)
      .on("end", dragended);
}
)}

function _d3(require){return(
require("d3@7")
)}

function uuidv4() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
    var r = Math.random() * 16 | 0, v = c == 'x' ? r : (r & 0x3 | 0x8);
    return v.toString(16);
  });
}

export default function define(runtime, observer) {
  let id = document.getElementById("id").value;
  let guid = uuidv4();
  const main = runtime.module();
  function toString() { return this.url; }
  const fileAttachments = new Map([
    ["sfhh@4.json", {url: new URL("./" + id + "/file?" + guid, import.meta.url), mimeType: "application/json", toString}]
  ]);
  main.builtin("FileAttachment", runtime.fileAttachments(name => fileAttachments.get(name)));
  main.variable(observer()).define(["md"], _1);
  main.variable(observer("viewof time")).define("viewof time", ["Scrubber","times"], _time);
  main.variable(observer("time")).define("time", ["Generators", "viewof time"], (G, _) => G.input(_));
  main.variable(observer("chart")).define("chart", ["d3","width","height","invalidation","drag"], _chart);
  main.variable(observer("update")).define("update", ["data","contains","time","chart"], _update);
  main.variable(observer("data")).define("data", ["FileAttachment"], _data);
  main.variable(observer("times")).define("times", ["d3","data","contains"], _times);
  main.variable(observer("contains")).define("contains", _contains);
  main.variable(observer("height")).define("height", _height);
  main.variable(observer("drag")).define("drag", ["d3"], _drag);
  main.variable(observer("d3")).define("d3", ["require"], _d3);
  const child1 = runtime.module(define1);
  main.import("Scrubber", child1);
  return main;
}
