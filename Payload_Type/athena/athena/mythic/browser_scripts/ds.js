function(task, responses){
    if(task.status.includes("error")){
        const combined = responses.reduce( (prev, cur) => {
            return prev + cur;
        }, "");
        return {'plaintext': combined};
    }else if(responses.length > 0){
        var response = "";
        for(let i = 0; i < responses.length; i++)
        {
            try{
                data = JSON.parse(responses[i]);
            }catch(error){
               const combined = responses.reduce( (prev, cur) => {
                    return prev + cur;
                }, "");
                return {'plaintext': combined};
            }

            task_response = responses[i];
            task_response.forEach(item => 
            {
                response += item["DistinguishedName"] + "\n";

                   attribs = item["Attributes"];
                   attribKeys = Object.keys(attribs);
                   
                   
                   attribKeys.forEach(function(key) {
                        attribute = attribs[key];
                        object_type = Object.prototype.toString.call(attribute);
                        value = ""
                        attribute.forEach(function(attr) {
                            value += atob(attr) + " | ";
                        });
                        response += key + ":" + value + "\n";
                   });
            });


        }
        return {"plaintext" : response};
    }else{
        return {"plaintext": "Task Not Returned."};
    }
}
