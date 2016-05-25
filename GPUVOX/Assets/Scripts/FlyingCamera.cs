// Converted from UnityScript to C# at http://www.M2H.nl/files/js_to_c.php - by Mike Hergaarden
// Do test the code! You usually need to change a few small bits.

using UnityEngine;
using System.Collections;

//namespace VOX
//{
	public class FlyingCamera : MonoBehaviour {
	
	
		public float mainSpeed = 40.0f; //regular speed
		
		public float shiftAdd = 100.0f; //multiplied by how long shift is held.  Basically running
		
		public float maxShift = 1000.0f; //Maximum speed when holdin gshift 
		
		public float camSens = 0.25f; //How sensitive it with mouse
		
		private Vector3 lastMouse = new Vector3(255, 255, 255); //kind of in the middle of the screen, rather than at the top (play)
		
		private float totalRun  = 1.0f;
	
		public float Rotation_Smooth = 0.3f;
		
		public float Acceleration_Smooth = 0.3f;
		
		private Vector3 prev_speed; 
	
		private Vector3 prev_rotate; 
		
		void  Update (){
			
			if (Input.GetMouseButton(2) ){ 
		
			    lastMouse = Vector3.Lerp(prev_rotate, Input.mousePosition - lastMouse, Rotation_Smooth ); 
			
				prev_rotate = lastMouse;
			
			    lastMouse = new Vector3(-lastMouse.y * camSens, lastMouse.x * camSens, 0 ); 
			
			    lastMouse = new Vector3(transform.eulerAngles.x + lastMouse.x , transform.eulerAngles.y + lastMouse.y, 0); 
			
			    transform.eulerAngles = lastMouse;

			}
			
			lastMouse =  Input.mousePosition;
			
		    //Mouse & camera angle done.  
		
		    //Keyboard commands

		    Vector3 p = GetBaseInput(); 
		
			
		    if (Input.GetKey (KeyCode.LeftShift)){ 
		
		        totalRun += Time.deltaTime; 
		
		        p  = p * totalRun * shiftAdd; 
		
		        p.x = Mathf.Clamp(p.x, -maxShift, maxShift); 
		
		        p.y = Mathf.Clamp(p.y, -maxShift, maxShift);
		
		        p.z = Mathf.Clamp(p.z, -maxShift, maxShift);
		
		    }
		
		    else{
		
		        totalRun = Mathf.Clamp(totalRun * 0.5f, 1, 1000); 
		
		        p = p * mainSpeed;
		
		    }
		
		    
		
		    p = Vector3.Lerp(prev_speed, p * Time.deltaTime, Acceleration_Smooth);
				   	    
		    transform.Translate( p ); 
		
		    prev_speed = p;
		
		}
		
		 
		
		private Vector3 GetBaseInput (){ //returns the basic values, if it's 0 than it's not active.
		
		    Vector3 p_Velocity = new Vector3();
		
		    if (Input.GetKey (KeyCode.W)){
		
		        p_Velocity += new Vector3(0, 0 , 1);
		
		    }
		
		    if (Input.GetKey (KeyCode.S)){
		
		        p_Velocity += new Vector3(0, 0 , -1);
		
		    }
		
		    if (Input.GetKey (KeyCode.A)){
		
		        p_Velocity += new Vector3(-1, 0 , 0);
		
		    }
		
		    if (Input.GetKey (KeyCode.D)){
		
		        p_Velocity += new Vector3(1, 0 , 0);
		
		    }
			if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.E)){ 
		        
		        //Move UP
		        p_Velocity += new Vector3(0, 1 , 0);
		
		    }
		    
		    if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.Q)){ 
		    
		        //Move DOWN
		        p_Velocity += new Vector3(0, -1 , 0);
		    }
		
		    return p_Velocity;	
		}
	}
