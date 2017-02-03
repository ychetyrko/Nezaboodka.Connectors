/*======================================
		Remove fields tests
 (based on `Add types and fields tests`)
======================================*/

USE `nz_test_closure`;

/*---------------------------------------/
	Remove single nested field
		without Back Reference
--------------------------------------*/
CALL before_alter_fields();
INSERT INTO `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('User', 'Login');
CALL remove_fields();

/*---------------------------------------/
	Remove single nested field
		with Back Reference
--------------------------------------*/
CALL before_alter_fields();
INSERT INTO `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('User', 'Group');
CALL remove_fields();

/*---------------------------------------/
	Remove single non-nested field
		with Back Reference
--------------------------------------*/
CALL before_alter_fields();
INSERT INTO `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('Group', 'Admin');
CALL remove_fields();

/*---------------------------------------/
	Remove multiple non-nested fields
		with(out) Back Reference
--------------------------------------*/
CALL before_alter_fields();
INSERT INTO `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('Admin', 'ControlGroup'),
('Group', 'Title'),
('UberAdmin', 'UberRating');
CALL remove_fields();

/*---------------------------------------/
	Remove nonexisting single field
		[Error expected]
--------------------------------------*/
CALL before_alter_fields();
INSERT INTO `field_rem_list`
(`owner_type_name`, `name`)
VALUES
('Admin', 'Login');
CALL remove_fields();
