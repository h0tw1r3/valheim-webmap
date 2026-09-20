const onPointers = (element, options) => {
  const downPointers = new Map();
  let downPointerArr = [];

  const down = (e) => {
    const newPointer = { downEvent: e, event: e };
    downPointers.set(e.pointerId, newPointer);
    downPointerArr.push(newPointer);
    if (options.down) {
      options.down(downPointerArr);
    }
  };

  const move = (e) => {
    const currentPointer = downPointers.get(e.pointerId);
    if (currentPointer) {
      currentPointer.event = e;
      if (options.move) {
        options.move(downPointerArr);
      }
    }
  };

  const up = (e) => {
    downPointers.delete(e.pointerId);
    downPointerArr = downPointerArr.filter(
      (pointer) => pointer.event.pointerId !== e.pointerId,
    );
    if (options.up) {
      options.up(downPointerArr);
    }
  };

  element.addEventListener("pointerdown", down);
  window.addEventListener("pointermove", move);
  window.addEventListener("pointerup", up);
  window.addEventListener("pointercancel", up);
};

export default onPointers;
