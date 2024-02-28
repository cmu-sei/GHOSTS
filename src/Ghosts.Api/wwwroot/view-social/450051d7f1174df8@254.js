function _1(md){return(
md`# Scrubber

This reusable input is intended to drive animations while providing the reader interactive control on demand: the animation pauses when the user interacts with the slider, but can be resumed by clicking the play button. For examples, see [Bar Chart Race](/@mbostock/bar-chart-race-with-scrubber), [The Wealth & Health of Nations](/@mbostock/the-wealth-health-of-nations), [Solar Path](/@mbostock/solar-path), or [Animated Treemap](/@d3/animated-treemap).`
)}

function _2(md){return(
md`To use in your notebook:

~~~js
import {Scrubber} from "@mbostock/scrubber"
~~~
`
)}

function _i(Scrubber,numbers){return(
Scrubber(numbers)
)}

function _numbers(){return(
Array.from({length: 256}, (_, i) => i)
)}

function _5(md,i){return(
md`The current value of *i* is ${i}.`
)}

function _6(md){return(
md`Given an array of *values* representing the discrete frames of the animation, such as an array of numbers or dates, Scrubber returns a [view-compatible input](/@observablehq/introduction-to-views). (It uses the [disposal promise](/@mbostock/disposal) to stop the animation automatically on invalidation.)`
)}

function _7(md){return(
md`## Options

Scrubber has several options which you can pass as the second argument.`
)}

function _autoplay(md){return(
md`The *autoplay* option, which defaults to true, specifies whether the animation plays automatically. Set it to false to require the reader to click on the play button.`
)}

function _9(Scrubber,numbers){return(
Scrubber(numbers, {autoplay: false})
)}

function _loop(md){return(
md`The *loop* option, which defaults to true, specifies whether the animation should automatically restart from the beginning after the end is reached. Set it to false to require the reader to click the play button to restart the animation after it ends.`
)}

function _11(Scrubber,numbers){return(
Scrubber(numbers, {loop: false})
)}

function _12(md){return(
md`The *alternate* option, which defaults to false, specifies whether the animation should reverse direction when it reaches the end, rather than repeat from the start.`
)}

function _13(Scrubber,numbers){return(
Scrubber(numbers, {loop: false, alternate: true})
)}

function _delay(md){return(
md`The *delay* option, which defaults to null, specifies how long to wait between frames in milliseconds. A null value means to use [requestAnimationFrame](https://developer.mozilla.org/en-US/docs/Web/API/window/requestAnimationFrame), which typically means sixty times per second (about 17ms). Non-null delays use [setInterval](https://developer.mozilla.org/en-US/docs/Web/API/WindowOrWorkerGlobalScope/setInterval).`
)}

function _15(Scrubber){return(
Scrubber(["red", "green", "blue"], {delay: 1000})
)}

function _loopDelay(md){return(
md`The *loopDelay* option, which defaults to 0, specifies how long to wait before looping in milliseconds. This can be paired with the *initial* option to show the ending value before the animation starts anew from the beginning.`
)}

function _17(Scrubber,numbers){return(
Scrubber(numbers, {initial: numbers.length - 1, loopDelay: 1000})
)}

function _format(md){return(
md`The *format* option, which defaults to the identity function, specifies how to display the currently-selected value. The *format* function is passed the current value, the current (zero-based) index, and the values array.`
)}

function _dates(){return(
Array.from({length: 365}, (_, i) => {
  const date = new Date(2019, 0, 1);
  date.setDate(i + 1);
  return date;
})
)}

function _date(Scrubber,dates){return(
Scrubber(dates, {
  autoplay: false,
  format: date => date.toLocaleString("en", {month: "long", day: "numeric"})
})
)}

function _21(md){return(
md`If you have suggestions for other options you’d like to see, please let me know!`
)}

function _22(md){return(
md`---

## Implementation`
)}

function _Scrubber(html,Inputs){return(
function Scrubber(values, {
  format = value => value,
  initial = 0,
  delay = null,
  autoplay = true,
  loop = true,
  loopDelay = null,
  alternate = false
} = {}) {
  values = Array.from(values);
  const form = html`<form style="font: 12px var(--sans-serif); font-variant-numeric: tabular-nums; display: flex; height: 33px; align-items: center;">
  <button name=b type=button style="margin-right: 0.4em; width: 5em;"></button>
  <label style="display: flex; align-items: center;">
    <input name=i type=range min=0 max=${values.length - 1} value=${initial} step=1 style="width: 180px;">
    <output name=o style="margin-left: 0.4em;"></output>
  </label>
</form>`;
  let frame = null;
  let timer = null;
  let interval = null;
  let direction = 1;
  function start() {
    form.b.textContent = "Pause";
    if (delay === null) frame = requestAnimationFrame(tick);
    else interval = setInterval(tick, delay);
  }
  function stop() {
    form.b.textContent = "Play";
    if (frame !== null) cancelAnimationFrame(frame), frame = null;
    if (timer !== null) clearTimeout(timer), timer = null;
    if (interval !== null) clearInterval(interval), interval = null;
  }
  function running() {
    return frame !== null || timer !== null || interval !== null;
  }
  function tick() {
    if (form.i.valueAsNumber === (direction > 0 ? values.length - 1 : direction < 0 ? 0 : NaN)) {
      if (!loop) return stop();
      if (alternate) direction = -direction;
      if (loopDelay !== null) {
        if (frame !== null) cancelAnimationFrame(frame), frame = null;
        if (interval !== null) clearInterval(interval), interval = null;
        timer = setTimeout(() => (step(), start()), loopDelay);
        return;
      }
    }
    if (delay === null) frame = requestAnimationFrame(tick);
    step();
  }
  function step() {
    form.i.valueAsNumber = (form.i.valueAsNumber + direction + values.length) % values.length;
    form.i.dispatchEvent(new CustomEvent("input", {bubbles: true}));
  }
  form.i.oninput = event => {
    if (event && event.isTrusted && running()) stop();
    form.value = values[form.i.valueAsNumber];
    form.o.value = format(form.value, form.i.valueAsNumber, values);
  };
  form.b.onclick = () => {
    if (running()) return stop();
    direction = alternate && form.i.valueAsNumber === values.length - 1 ? -1 : 1;
    form.i.valueAsNumber = (form.i.valueAsNumber + direction) % values.length;
    form.i.dispatchEvent(new CustomEvent("input", {bubbles: true}));
    start();
  };
  form.i.oninput();
  if (autoplay) start();
  else stop();
  Inputs.disposal(form).then(stop);
  return form;
}
)}

export default function define(runtime, observer) {
  const main = runtime.module();
  main.variable(observer()).define(["md"], _1);
  main.variable(observer()).define(["md"], _2);
  main.variable(observer("viewof i")).define("viewof i", ["Scrubber","numbers"], _i);
  main.variable(observer("i")).define("i", ["Generators", "viewof i"], (G, _) => G.input(_));
  main.variable(observer("numbers")).define("numbers", _numbers);
  main.variable(observer()).define(["md","i"], _5);
  main.variable(observer()).define(["md"], _6);
  main.variable(observer()).define(["md"], _7);
  main.variable(observer("autoplay")).define("autoplay", ["md"], _autoplay);
  main.variable(observer()).define(["Scrubber","numbers"], _9);
  main.variable(observer("loop")).define("loop", ["md"], _loop);
  main.variable(observer()).define(["Scrubber","numbers"], _11);
  main.variable(observer()).define(["md"], _12);
  main.variable(observer()).define(["Scrubber","numbers"], _13);
  main.variable(observer("delay")).define("delay", ["md"], _delay);
  main.variable(observer()).define(["Scrubber"], _15);
  main.variable(observer("loopDelay")).define("loopDelay", ["md"], _loopDelay);
  main.variable(observer()).define(["Scrubber","numbers"], _17);
  main.variable(observer("format")).define("format", ["md"], _format);
  main.variable(observer("dates")).define("dates", _dates);
  main.variable(observer("viewof date")).define("viewof date", ["Scrubber","dates"], _date);
  main.variable(observer("date")).define("date", ["Generators", "viewof date"], (G, _) => G.input(_));
  main.variable(observer()).define(["md"], _21);
  main.variable(observer()).define(["md"], _22);
  main.variable(observer("Scrubber")).define("Scrubber", ["html","Inputs"], _Scrubber);
  return main;
}
