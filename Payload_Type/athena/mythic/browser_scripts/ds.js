function(task, responses){
    if(task.status.includes("error")){
        const combined = responses.reduce( (prev, cur) => {
            return prev + cur;
        }, "");
        return {'plaintext': combined};
    }else if(responses.length > 0){
        let file = {};
        let data = "";
        let rows = [];
        let headers = [
            {"plaintext": "name", "type": "string", "cellStyle": {}},
            {"plaintext": "value", "type": "string", "cellStyle": {}},
        ];
        for(let i = 0; i < responses.length; i++)
        {
            console.log(responses+[i])
            try{
                data = JSON.parse(responses[i]);
            }catch(error){
               const combined = responses.reduce( (prev, cur) => {
                    return prev + cur;
                }, "");
                return {'plaintext': combined};
            }
            
            for(let j = 0; j < data.results.length; j++){
                let pinfo = data.results[j];

                for(let k = 0; k < pinfo.length; k++){
                    let rinfo = pinfo[k];
                    let row = {
                        "rowStyle": {},
                        "name": {"plaintext": rinfo[0], "cellStyle": {}},
                        "value": {"plaintext": rinfo[1], "cellStyle": {}},
                    };
                    rows.push(row);
                }
                let row = {
                    "rowStyle": {},
                    "name": {"plaintext": "", "cellStyle": {}},
                    "value": {"plaintext": "", "cellStyle": {}},
                }
            }
        }
        return {"table":[{
            "headers": headers,
            "rows": rows,
            "title": "ds results"
        }]};
    }else{
        return {"plaintext": "Task Not Returned."};
    }
}
