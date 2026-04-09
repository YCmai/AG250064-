const s={1:"上料",2:"下料",3:"空储位到上料架"},p={1:"搬运",2:"充电"},o=(a,e)=>(e==="NDC"?p:s)[a]||`未知类型(${a})`,c=a=>Object.entries(a==="NDC"?p:s).map(([t,n])=>({value:Number(t),label:n}));export{c as a,o as g};
