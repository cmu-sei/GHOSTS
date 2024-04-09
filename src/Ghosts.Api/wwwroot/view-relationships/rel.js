// https://observablehq.com/@d3/mobile-patent-suits@219
import define1 from "./rel_lib.js";

function uuidv4() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
        var r = Math.random() * 16 | 0, v = c == 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}

function GetNpcUrl(o, id) {
    const x = o.find(x => x.target.toLowerCase() === id.toLowerCase());
    if (x === undefined)
        return "return null;";
    return "window.open('./view-relationships/profile/" + x.npc_id + "')";
}

function GetNpcPhoto(o, id) {
    const x = o.find(x => x.target.toLowerCase() === id.toLowerCase());
    if (x === undefined)
        return "/bg.jpg";
    return "/api/npcs/" + x.npc_id + "/photo";
}

function GetNpcId(o, id) {
    console.log(id)
    const x = o.find(x => x.target.toLowerCase() === id.toLowerCase());
    if (x === undefined)
        return "";
    return x.npc_id;
}

var id = uuidv4();

export default function define(runtime, observer) {
    const main = runtime.module();
    const fileAttachments = new Map([
        ["data1.csv?id=" + id, new URL("/animator/view-relationships/files/data1.csv?id=" + id,
            import.meta.url)]
    ]);
    main.builtin("FileAttachment", runtime.fileAttachments(name => fileAttachments.get(name)));
    main.variable(observer()).define(["md"], function (md) {
        return (
            md``
        )
    });
    main.variable(observer()).define(["swatches", "color"], function (swatches, color) {
        return (
            swatches({
                color
            })
        )
    });
    main.variable(observer("chart")).define("chart", ["data", "d3", "width", "height", "types", "color", "location", "drag", "linkArc", "invalidation"], function (data, d3, width, height, types, color, location, drag, linkArc, invalidation) {
        const links = data.links.map(d => Object.create(d));
        const nodes = data.nodes.map(d => Object.create(d));

        const simulation = d3.forceSimulation(nodes)
            .force("link", d3.forceLink(links).id(d => d.id))
            .force("charge", d3.forceManyBody().strength(-400))
            .force("x", d3.forceX())
            .force("y", d3.forceY());

        const svg = d3.create("svg")
            .attr("viewBox", [-width / 2, -height / 2, width, height])
            .style("font", "12px sans-serif");

        // Per-type markers, as they don't inherit styles.
        svg.append("defs").selectAll("marker")
            .data(types)
            .join("marker")
            .attr("id", d => `arrow-${d}`)
            .attr("viewBox", "0 -5 10 10")
            .attr("refX", 15)
            .attr("refY", -0.5)
            .attr("markerWidth", 3)
            .attr("markerHeight", 3)
            .attr("orient", "auto")
            .append("path")
            .attr("fill", color)
            .attr("d", "M0,-5L10,0L0,5");

        const link = svg.append("g")
            .attr("fill", "none")
            .attr("stroke-width", 3.5)
            .selectAll("path")
            .data(links)
            .join("path")
            .attr("stroke", d => color(d.type))
            .attr("marker-end", d => `url(${new URL(`#arrow-${d.type}`, location)})`);

        const node = svg.append("g")
            .attr("fill", "currentColor")
            .attr("stroke-linecap", "round")
            .attr("stroke-linejoin", "round")
            .selectAll("g")
            .data(nodes)
            .join("g")
            .call(drag(simulation));

        node.append("defs")
            .append("pattern")
            .attr("id", d => `d` + GetNpcId(data.links, d.id))
            .attr("height", "100%")
            .attr("width", "100%")
            .attr("patternContenUnits", "objectBoundingBox")
            .append("image")
            .attr("height", 45)
            .attr("width", 45)
            .attr("preserveAspectRatio", "none")
            .attr("xlink:href", d => GetNpcPhoto(data.links, d.id))

        node.append("circle")
            .attr("r", 20)
            .style("stroke", "skyblue")
            .style("stroke-width", 2.25)
            .attr("fill", d => `url(#d` + GetNpcId(data.links, d.id)) + `)"`;

        node.append("text")
            .attr("class", "link")
            .attr("onclick", d => GetNpcUrl(data.links, d.id))
            .attr("x", 25)
            .attr("y", "0.31em")
            .text(d => d.id)
            .clone(true).lower()
            .attr("fill", "none")
            .attr("stroke", "white")
            .attr("stroke-width", 3)

        simulation.on("tick", () => {
            link.attr("d", linkArc);
            node.attr("transform", d => `translate(${d.x},${d.y})`);
        });

        invalidation.then(() => simulation.stop());

        return svg.node();
    });
    main.variable(observer("links")).define("links", ["d3", "FileAttachment"], async function (d3, FileAttachment) {
        return (
            d3.csvParse(await FileAttachment("/animator/view-relationships/files/data1.csv?id=" + id).text())
        )
    });
    main.variable(observer("types")).define("types", ["links"], function (links) {
        return (
            Array.from(new Set(links.map(d => d.type)))
        )
    });
    main.variable(observer("data")).define("data", ["links"], function (links) {
        return ({
            nodes: Array.from(new Set(links.flatMap(l => [l.source, l.target])), id => ({id})),
            links
        })
    });
    main.variable(observer("height")).define("height", function () {
        return (
            1100
        )
    });
    main.variable(observer("color")).define("color", ["d3", "types"], function (d3, types) {
        return (
            d3.scaleOrdinal(types, d3.schemeCategory10)
        )
    });
    main.variable(observer("linkArc")).define("linkArc", function () {
        return (
            function linkArc(d) {
                const r = Math.hypot(d.target.x - d.source.x, d.target.y - d.source.y);
                return `
    M${d.source.x},${d.source.y}
    A${r},${r} 0 0,1 ${d.target.x},${d.target.y}
  `;
            }
        )
    });
    main.variable(observer("drag")).define("drag", ["d3"], function (d3) {
        return (
            simulation => {

                function dragstarted(d) {
                    if (!d3.event.active) simulation.alphaTarget(0.3).restart();
                    d.fx = d.x;
                    d.fy = d.y;
                }

                function dragged(d) {
                    d.fx = d3.event.x;
                    d.fy = d3.event.y;
                }

                function dragended(d) {
                    if (!d3.event.active) simulation.alphaTarget(0);
                    d.fx = null;
                    d.fy = null;
                }

                return d3.drag()
                    .on("start", dragstarted)
                    .on("drag", dragged)
                    .on("end", dragended);
            }
        )
    });
    main.variable(observer("d3")).define("d3", ["require"], function (require) {
        return (
            require("d3@5")
        )
    });
    const child1 = runtime.module(define1);
    main.import("swatches", child1);
    return main;
}